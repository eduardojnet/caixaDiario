using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace CaixaDiario.Consolidado.Api;

/// <summary>
/// Consome LancamentoRegistrado v1 e atualiza o modelo de leitura. Entrega ao
/// menos uma vez: o ack so sai depois do COMMIT, e a reentrega e descartada
/// pela chave do lancamento (RF-10).
/// </summary>
public sealed partial class ProjetorRabbitMq(
    RepositorioConsolidado repositorio,
    EstadoDoProjetor estado,
    Telemetria telemetria,
    IOptions<OpcoesBroker> opcoes,
    ILogger<ProjetorRabbitMq> logger) : BackgroundService
{
    private readonly OpcoesBroker _opcoes = opcoes.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var falhas = 0;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ConsumirAteCairAsync(() => falhas = 0, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
#pragma warning disable CA1031 // Broker fora vira espera e reconexao; o servico de consulta continua de pe.
            catch (Exception ex)
#pragma warning restore CA1031
            {
                falhas++;
                LogFalhaDeConexao(logger, ex, falhas);
            }

            var espera = TimeSpan.FromMilliseconds(Math.Min(250 * Math.Pow(2, Math.Min(falhas, 10)), 10_000));
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

    private async Task ConsumirAteCairAsync(Action aoConectar, CancellationToken stoppingToken)
    {
        var fabrica = new ConnectionFactory
        {
            HostName = _opcoes.Host,
            Port = _opcoes.Porta,
            UserName = _opcoes.Usuario,
            Password = _opcoes.Senha,
            ClientProvidedName = "caixadiario-consolidado",
            RequestedConnectionTimeout = TimeSpan.FromSeconds(3),
            AutomaticRecoveryEnabled = false,
        };

        await using var conexao = await fabrica.CreateConnectionAsync(stoppingToken).ConfigureAwait(false);
        await using var canal = await conexao.CreateChannelAsync(cancellationToken: stoppingToken).ConfigureAwait(false);

        await canal.ExchangeDeclareAsync(_opcoes.Exchange, ExchangeType.Fanout, durable: true, autoDelete: false,
            cancellationToken: stoppingToken).ConfigureAwait(false);
        await canal.QueueDeclareAsync(_opcoes.Fila, durable: true, exclusive: false, autoDelete: false,
            cancellationToken: stoppingToken).ConfigureAwait(false);
        await canal.QueueBindAsync(_opcoes.Fila, _opcoes.Exchange, routingKey: string.Empty,
            cancellationToken: stoppingToken).ConfigureAwait(false);
        await canal.BasicQosAsync(0, _opcoes.Prefetch, global: false, stoppingToken).ConfigureAwait(false);

        var caiu = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        conexao.ConnectionShutdownAsync += (_, _) =>
        {
            caiu.TrySetResult();
            return Task.CompletedTask;
        };

        var consumidor = new AsyncEventingBasicConsumer(canal);
        consumidor.ReceivedAsync += async (_, entrega) =>
            await TratarAsync(canal, entrega, stoppingToken).ConfigureAwait(false);

        await canal.BasicConsumeAsync(_opcoes.Fila, autoAck: false, consumidor, stoppingToken).ConfigureAwait(false);
        aoConectar();
        LogConsumindo(logger, _opcoes.Fila);

        // Canal separado so para medir a fila: base da marca 'defasado'.
        await using var monitor = await conexao.CreateChannelAsync(cancellationToken: stoppingToken).ConfigureAwait(false);
        try
        {
            estado.Conectou();
            while (!caiu.Task.IsCompleted)
            {
                var fila = await monitor.QueueDeclarePassiveAsync(_opcoes.Fila, stoppingToken).ConfigureAwait(false);
                estado.AtualizarPendentes(fila.MessageCount);
                await Task.WhenAny(caiu.Task, Task.Delay(TimeSpan.FromSeconds(1), stoppingToken)).ConfigureAwait(false);
                stoppingToken.ThrowIfCancellationRequested();
            }
        }
        finally
        {
            estado.Desconectou();
        }
    }

    private async Task TratarAsync(IChannel canal, BasicDeliverEventArgs entrega, CancellationToken cancellationToken)
    {
        // Continua o traco iniciado no POST de Lancamentos (ADR-0003).
        ActivityContext.TryParse(LerTraceparent(entrega.BasicProperties), null, out var origem);
        using var atividade = Telemetria.Fonte.StartActivity("projetor aplicar", ActivityKind.Consumer, origem);
        atividade?.SetTag("messaging.system", "rabbitmq");
        atividade?.SetTag("messaging.message.id", entrega.BasicProperties.MessageId);

        var evento = EventoLancamentoRegistradoV1.Ler(entrega.Body.Span);
        if (evento is null)
        {
            // Mensagem invalida nao volta para a fila: o lancamento continua
            // integro em Lancamentos e o consolidado e reconstruivel.
            LogMensagemInvalida(logger, entrega.BasicProperties.MessageId);
            await canal.BasicRejectAsync(entrega.DeliveryTag, requeue: false, cancellationToken).ConfigureAwait(false);
            return;
        }

        try
        {
            var aplicado = await repositorio.AplicarAsync(evento, cancellationToken).ConfigureAwait(false);
            atividade?.SetTag("caixadiario.lancamento_id", evento.LancamentoId.ToString());
            atividade?.SetTag("caixadiario.reentrega", !aplicado);
            if (aplicado)
            {
                telemetria.Aplicados.Add(1);
                estado.RegistrarDefasagem(DateTimeOffset.UtcNow - evento.RegistradoEm);
            }
            else
            {
                telemetria.Reentregas.Add(1);
                LogReentrega(logger, evento.LancamentoId);
            }

            await canal.BasicAckAsync(entrega.DeliveryTag, multiple: false, cancellationToken).ConfigureAwait(false);
        }
#pragma warning disable CA1031 // Falha ao aplicar devolve a mensagem para a fila; nada se perde.
        catch (Exception ex) when (ex is not OperationCanceledException)
#pragma warning restore CA1031
        {
            atividade?.SetStatus(ActivityStatusCode.Error, ex.Message);
            LogFalhaAoAplicar(logger, ex, evento.LancamentoId);
            await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken).ConfigureAwait(false);
            await canal.BasicNackAsync(entrega.DeliveryTag, multiple: false, requeue: true, cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private static string? LerTraceparent(IReadOnlyBasicProperties propriedades) =>
        propriedades.Headers is { } cabecalhos && cabecalhos.TryGetValue("traceparent", out var valor)
            ? valor switch
            {
                byte[] bytes => Encoding.UTF8.GetString(bytes),
                string texto => texto,
                _ => null,
            }
            : null;

    [LoggerMessage(Level = LogLevel.Warning, Message = "Projetor: broker indisponivel ({Falhas} falhas seguidas); o consolidado fica defasado ate reconectar")]
    private static partial void LogFalhaDeConexao(ILogger logger, Exception ex, int falhas);

    [LoggerMessage(Level = LogLevel.Information, Message = "Projetor: consumindo a fila {Fila}")]
    private static partial void LogConsumindo(ILogger logger, string fila);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Projetor: mensagem {MessageId} invalida, rejeitada sem reenfileirar")]
    private static partial void LogMensagemInvalida(ILogger logger, string? messageId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Projetor: lancamento {LancamentoId} ja aplicado, reentrega descartada")]
    private static partial void LogReentrega(ILogger logger, Guid lancamentoId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Projetor: falha ao aplicar o lancamento {LancamentoId}; mensagem devolvida a fila")]
    private static partial void LogFalhaAoAplicar(ILogger logger, Exception ex, Guid lancamentoId);
}
