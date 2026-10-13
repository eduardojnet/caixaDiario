using System.Text.Json;
using System.Text.Json.Serialization;

namespace CaixaDiario.Lancamentos.Api;

/// <summary>
/// Contrato publicado, docs/contratos/lancamento-registrado.v1.md. Tipo proprio
/// deste servico: nao ha assembly compartilhado com o consumidor (ADR-0002); o
/// teste de contrato compara com o exemplo JSON versionado.
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
    public const string NomeDoTipo = "LancamentoRegistrado.v1";

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    public string Serializar() => JsonSerializer.Serialize(this, Json);
}
