using System.Diagnostics;
using Dapper;
using Microsoft.Extensions.Options;
using Npgsql;

namespace CaixaDiario.Lancamentos.Api;

/// <summary>
/// Le a outbox e publica os eventos pendentes, em segundo plano. Fora do
/// caminho do registro: se o broker estiver fora, o POST continua respondendo
/// 201 e os eventos acumulam na outbox ate o broker voltar (RNF-01).
/// </summary>
/// <remarks>
/// Entrega ao menos uma vez: se o processo cair entre publicar e marcar, o
/// evento e publicado de novo, e o consumidor descarta a duplicata pela chave
/// unica do lancamento. Varias instancias convivem pelo SKIP LOCKED.
/// </remarks>
public sealed partial class DespachanteOutbox(
    NpgsqlDataSource dataSource,
    PublicadorRabbitMq publicador,
    Telemetria telemetria,
    IOptions<OpcoesDespacho> opcoes,
    ILogger<DespachanteOutbox> logger) : BackgroundService
{
    private readonly OpcoesDespacho _opcoes = opcoes.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var falhasSeguidas = 0;
        var proximaMedida = DateTime.MinValue;

        while (!stoppingToken.IsCancellationRequested)
        {
            // Medida da outbox independente do broker: e justamente com o
            // broker fora que ela precisa continuar sendo informada.
            if (DateTime.UtcNow >= proximaMedida)
            {
                await MedirOutboxAsync(stoppingToken).ConfigureAwait(false);
                proximaMedida = DateTime.UtcNow.AddSeconds(5);
            }

            TimeSpan espera;
            try
            {
                var publicados = await DespacharLoteAsync(stoppingToken).ConfigureAwait(false);
                if (falhasSeguidas > 0)
                {
                    LogBrokerDeVolta(logger, falhasSeguidas);
                }

                falhasSeguidas = 0;
                espera = publicados == _opcoes.TamanhoDoLote ? TimeSpan.Zero : _opcoes.Intervalo;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
#pragma warning disable CA1031 // Qualquer falha de broker ou banco vira espera e nova tentativa; o servico nao pode morrer por isso.
            catch (Exception ex)
#pragma warning restore CA1031
            {
                falhasSeguidas++;
                espera = EsperaCrescente(falhasSeguidas);
                LogFalhaNoDespacho(logger, ex, falhasSeguidas, espera.TotalMilliseconds);
                await publicador.DescartarAsync().ConfigureAwait(false);
            }

            try
            {
                await Task.Delay(espera, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    public async Task<int> DespacharLoteAsync(CancellationToken cancellationToken)
    {
        await using var conexao = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var transacao = await conexao.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        var pendentes = (await conexao.QueryAsync<Pendente>(new CommandDefinition(
            """
            SELECT id AS Id, evento_id AS EventoId, tipo_evento AS TipoEvento, payload::text AS Payload,
                   traceparent AS Traceparent
            FROM outbox
            WHERE publicado_em IS NULL
            ORDER BY id
            LIMIT @limite
            FOR UPDATE SKIP LOCKED
            """,
            new { limite = _opcoes.TamanhoDoLote }, transacao, cancellationToken: cancellationToken)).ConfigureAwait(false)).AsList();

        if (pendentes.Count == 0)
        {
            await transacao.CommitAsync(cancellationToken).ConfigureAwait(false);
            return 0;
        }

        foreach (var evento in pendentes)
        {
            // Continua o traco da requisicao que registrou o lancamento.
            ActivityContext.TryParse(evento.Traceparent, null, out var origem);
            using var atividade = Telemetria.Fonte.StartActivity("outbox publicar", ActivityKind.Producer, origem);
            atividade?.SetTag("messaging.system", "rabbitmq");
            atividade?.SetTag("messaging.message.id", evento.EventoId.ToString());

            await publicador.PublicarAsync(
                evento.EventoId, evento.TipoEvento, evento.Payload, atividade?.Id ?? evento.Traceparent, cancellationToken)
                .ConfigureAwait(false);
        }

        await conexao.ExecuteAsync(new CommandDefinition(
            "UPDATE outbox SET publicado_em = now() WHERE id = ANY(@ids)",
            new { ids = pendentes.Select(p => p.Id).ToArray() }, transacao, cancellationToken: cancellationToken))
            .ConfigureAwait(false);

        await transacao.CommitAsync(cancellationToken).ConfigureAwait(false);
        telemetria.Publicados.Add(pendentes.Count);
        LogLotePublicado(logger, pendentes.Count);
        return pendentes.Count;
    }

    private async Task MedirOutboxAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var conexao = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            var medida = await conexao.QuerySingleAsync<(long Pendentes, double IdadeSegundos)>(new CommandDefinition(
                """
                SELECT count(*), COALESCE(EXTRACT(EPOCH FROM now() - min(criado_em)), 0)::float8
                FROM outbox WHERE publicado_em IS NULL
                """,
                cancellationToken: cancellationToken)).ConfigureAwait(false);
            telemetria.AtualizarOutbox(medida.Pendentes, medida.IdadeSegundos);
        }
        catch (NpgsqlException ex)
        {
            LogFalhaNaMedida(logger, ex);
        }
    }

    private TimeSpan EsperaCrescente(int falhas)
    {
        var milissegundos = 250 * Math.Pow(2, Math.Min(falhas, 10));
        return TimeSpan.FromMilliseconds(Math.Min(milissegundos, _opcoes.EsperaMaxima.TotalMilliseconds));
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Outbox: {Quantidade} eventos publicados")]
    private static partial void LogLotePublicado(ILogger logger, int quantidade);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Outbox: falha no despacho ({Falhas} seguidas); nova tentativa em {EsperaMs} ms. Os eventos continuam pendentes")]
    private static partial void LogFalhaNoDespacho(ILogger logger, Exception ex, int falhas, double esperaMs);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Outbox: nao foi possivel medir a fila pendente")]
    private static partial void LogFalhaNaMedida(ILogger logger, Exception ex);

    [LoggerMessage(Level = LogLevel.Information, Message = "Outbox: despacho retomado depois de {Falhas} falhas")]
    private static partial void LogBrokerDeVolta(ILogger logger, int falhas);

    private sealed class Pendente
    {
        public long Id { get; set; }

        public Guid EventoId { get; set; }

        public string TipoEvento { get; set; } = string.Empty;

        public string Payload { get; set; } = string.Empty;

        public string? Traceparent { get; set; }
    }
}
