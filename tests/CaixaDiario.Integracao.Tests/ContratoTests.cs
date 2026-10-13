using System.Text.Json;
using System.Text.Json.Nodes;

namespace CaixaDiario.Integracao.Tests;

/// <summary>
/// Os dois lados validam contra o MESMO exemplo versionado
/// (docs/contratos/lancamento-registrado.v1.exemplo.json), sem tipo compartilhado.
/// </summary>
public sealed class ContratoTests
{
    private static readonly string Exemplo = File.ReadAllText(
        Path.Combine(AppContext.BaseDirectory, "contratos", "lancamento-registrado.v1.exemplo.json"));

    private static HashSet<string> Campos(string json) =>
        [.. JsonNode.Parse(json)!.AsObject().Select(p => p.Key)];

    [Fact]
    public void Produtor_publica_exatamente_os_campos_do_contrato()
    {
        var evento = JsonSerializer.Deserialize<Lancamentos.Api.EventoLancamentoRegistradoV1>(
            Exemplo, Lancamentos.Api.EventoLancamentoRegistradoV1.Json)!;

        Assert.Equal(Campos(Exemplo), Campos(evento.Serializar()));
        Assert.Equal(150.75m, evento.Valor);
        Assert.Equal("CREDITO", evento.Tipo);
        Assert.Equal(new DateOnly(2026, 10, 12), evento.DataDoFato);
    }

    [Fact]
    public void Consumidor_le_o_exemplo_do_contrato()
    {
        var evento = Consolidado.Api.EventoLancamentoRegistradoV1.Ler(System.Text.Encoding.UTF8.GetBytes(Exemplo));

        Assert.NotNull(evento);
        Assert.Equal(Guid.Parse("7c9e6679-7425-40de-944b-e07fc1f90ae7"), evento.LancamentoId);
        Assert.Equal("loja-centro", evento.CaixaId);
        Assert.Equal(150.75m, evento.Valor);
        Assert.Equal(new DateOnly(2026, 10, 12), evento.DataDoFato);
    }

    [Theory]
    [InlineData("\"versao\": 1", "\"versao\": 2")]
    [InlineData("\"CREDITO\"", "\"PIX\"")]
    [InlineData("150.75", "-1")]
    [InlineData("{", "nao e json")]
    public void Consumidor_recusa_mensagem_fora_do_contrato(string original, string troca)
    {
        var corpo = Exemplo.Replace(original, troca, StringComparison.Ordinal);

        Assert.Null(Consolidado.Api.EventoLancamentoRegistradoV1.Ler(System.Text.Encoding.UTF8.GetBytes(corpo)));
    }

    [Fact]
    public void Consumidor_ignora_campo_opcional_novo()
    {
        var corpo = Exemplo.Replace("\"versao\": 1,", "\"versao\": 1, \"canal\": \"pix\",", StringComparison.Ordinal);

        Assert.NotNull(Consolidado.Api.EventoLancamentoRegistradoV1.Ler(System.Text.Encoding.UTF8.GetBytes(corpo)));
    }
}
