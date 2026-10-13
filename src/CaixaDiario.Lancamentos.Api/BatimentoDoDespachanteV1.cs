using System.Text.Json;

namespace CaixaDiario.Lancamentos.Api;

/// <summary>
/// Sinal de vida do despachante com o estado da outbox,
/// docs/contratos/batimento-do-despachante.v1.md (cartao 43).
/// </summary>
public sealed record BatimentoDoDespachanteV1(
    int Versao,
    DateTimeOffset EmitidoEm,
    long OutboxPendentes,
    double OutboxIdadeMaisAntigaSegundos)
{
    public const string NomeDoTipo = "BatimentoDoDespachante.v1";

    public string Serializar() => JsonSerializer.Serialize(this, EventoLancamentoRegistradoV1.Json);
}
