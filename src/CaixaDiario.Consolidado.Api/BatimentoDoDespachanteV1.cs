using System.Text.Json;

namespace CaixaDiario.Consolidado.Api;

/// <summary>
/// Leitura do batimento do despachante, docs/contratos/batimento-do-despachante.v1.md.
/// Tipo proprio do consumidor, sem assembly compartilhado.
/// </summary>
public sealed record BatimentoDoDespachanteV1(
    int Versao,
    DateTimeOffset EmitidoEm,
    long OutboxPendentes,
    double OutboxIdadeMaisAntigaSegundos)
{
    public const string NomeDoTipo = "BatimentoDoDespachante.v1";

    /// <summary>Devolve null para batimento malformado, de outra versao ou com valor invalido.</summary>
    public static BatimentoDoDespachanteV1? Ler(ReadOnlySpan<byte> corpo)
    {
        try
        {
            var batimento = JsonSerializer.Deserialize<BatimentoDoDespachanteV1>(corpo, EventoLancamentoRegistradoV1.Json);
            return batimento is { Versao: 1, OutboxPendentes: >= 0, OutboxIdadeMaisAntigaSegundos: >= 0 } ? batimento : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
