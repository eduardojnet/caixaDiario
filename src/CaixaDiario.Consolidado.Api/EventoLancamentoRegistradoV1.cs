using System.Text.Json;

namespace CaixaDiario.Consolidado.Api;

/// <summary>
/// Leitura do contrato docs/contratos/lancamento-registrado.v1.md. Tipo proprio
/// do consumidor: o produtor pode evoluir com campo opcional novo sem quebrar
/// este lado, que ignora o que nao conhece.
/// </summary>
public sealed record EventoLancamentoRegistradoV1(
    Guid EventoId,
    int Versao,
    Guid LancamentoId,
    string CaixaId,
    string Tipo,
    decimal Valor,
    DateOnly DataDoFato,
    DateTimeOffset RegistradoEm)
{
    public const string Credito = "CREDITO";
    public const string Debito = "DEBITO";

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>Devolve null para mensagem malformada, de outra versao ou com dado invalido.</summary>
    public static EventoLancamentoRegistradoV1? Ler(ReadOnlySpan<byte> corpo)
    {
        EventoLancamentoRegistradoV1? evento;
        try
        {
            evento = JsonSerializer.Deserialize<EventoLancamentoRegistradoV1>(corpo, Json);
        }
        catch (JsonException)
        {
            return null;
        }

        return evento is { Versao: 1 }
            && evento.LancamentoId != Guid.Empty
            && !string.IsNullOrWhiteSpace(evento.CaixaId)
            && evento.Tipo is Credito or Debito
            && evento.Valor > 0
            ? evento
            : null;
    }
}
