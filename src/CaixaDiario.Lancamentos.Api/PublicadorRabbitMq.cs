using System.Text;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace CaixaDiario.Lancamentos.Api;

/// <summary>
/// Publica eventos no broker com confirmacao. A conexao e aberta sob demanda
/// pelo despachante, nunca na subida da API: broker fora nao impede o
/// registro de lancamentos (RNF-01).
/// </summary>
public sealed class PublicadorRabbitMq(IOptions<OpcoesBroker> opcoes) : IAsyncDisposable
{
    private readonly OpcoesBroker _opcoes = opcoes.Value;
    private IConnection? _conexao;
    private IChannel? _canal;

    public async Task PublicarAsync(
        Guid eventoId, string tipoEvento, string payload, string? traceparent, CancellationToken cancellationToken)
    {
        var canal = await ObterCanalAsync(cancellationToken).ConfigureAwait(false);
        var propriedades = new BasicProperties
        {
            MessageId = eventoId.ToString(),
            Type = tipoEvento,
            ContentType = "application/json",
            DeliveryMode = DeliveryModes.Persistent,
        };

        // Propagacao W3C pelo cabecalho da mensagem: o projetor do Consolidado
        // continua o mesmo traco (ADR-0003).
        if (traceparent is not null)
        {
            propriedades.Headers = new Dictionary<string, object?> { ["traceparent"] = traceparent };
        }

        // Com confirmacao habilitada no canal, a chamada so retorna depois do
        // ack do broker; nack ou queda lancam excecao, e o evento fica pendente.
        // mandatory: sem fila ligada a exchange (Consolidado ainda nao subiu), o
        // broker devolve a mensagem, a chamada falha e o evento fica na outbox
        // em vez de ser descartado.
        await canal.BasicPublishAsync(
            _opcoes.Exchange, routingKey: string.Empty, mandatory: true, propriedades,
            Encoding.UTF8.GetBytes(payload), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Descarta a conexao depois de uma falha; a proxima publicacao reconecta.</summary>
    public async Task DescartarAsync()
    {
        var canal = _canal;
        var conexao = _conexao;
        _canal = null;
        _conexao = null;

        if (canal is not null)
        {
            await canal.DisposeAsync().ConfigureAwait(false);
        }

        if (conexao is not null)
        {
            await conexao.DisposeAsync().ConfigureAwait(false);
        }
    }

    public async ValueTask DisposeAsync() => await DescartarAsync().ConfigureAwait(false);

    private async Task<IChannel> ObterCanalAsync(CancellationToken cancellationToken)
    {
        if (_canal is { IsOpen: true })
        {
            return _canal;
        }

        await DescartarAsync().ConfigureAwait(false);

        var fabrica = new ConnectionFactory
        {
            HostName = _opcoes.Host,
            Port = _opcoes.Porta,
            UserName = _opcoes.Usuario,
            Password = _opcoes.Senha,
            ClientProvidedName = "caixadiario-lancamentos",
            RequestedConnectionTimeout = TimeSpan.FromSeconds(3),
            // A reconexao e conduzida pelo despachante, com espera crescente.
            AutomaticRecoveryEnabled = false,
        };

        _conexao = await fabrica.CreateConnectionAsync(cancellationToken).ConfigureAwait(false);
        _canal = await _conexao.CreateChannelAsync(
            new CreateChannelOptions(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true),
            cancellationToken).ConfigureAwait(false);

        // O produtor declara so a exchange; filas sao do consumidor.
        await _canal.ExchangeDeclareAsync(
            _opcoes.Exchange, ExchangeType.Fanout, durable: true, autoDelete: false,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return _canal;
    }
}
