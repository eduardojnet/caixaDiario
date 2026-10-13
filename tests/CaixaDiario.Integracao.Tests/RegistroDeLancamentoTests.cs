using System.Net;
using System.Net.Http.Json;
using CaixaDiario.Integracao.Tests.Infra;
using CaixaDiario.Lancamentos.Api;
using Dapper;
using Npgsql;

namespace CaixaDiario.Integracao.Tests;

/// <summary>Idempotencia (RNF-07), estorno (RF-05) e validacao, contra banco real.</summary>
public sealed class RegistroDeLancamentoTests(BancoLancamentosFixture banco) : IAsyncLifetime
{
    private FabricaLancamentos? _api;
    private HttpClient? _cliente;

    private HttpClient Cliente => _cliente!;

    public ValueTask InitializeAsync()
    {
        _api = new FabricaLancamentos(banco.ConexaoAplicacao, Broker.Ausente(Utilidades.PortaLivre()));
        _cliente = _api.CreateClient();
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        _cliente?.Dispose();
        if (_api is not null)
        {
            await _api.DisposeAsync();
        }
    }

    [Fact]
    public async Task Mesma_chave_e_mesmo_corpo_geram_um_lancamento_so()
    {
        var caixa = Utilidades.NovoCaixa();

        using var primeira = await Cliente.LancarAsync(caixa, "chave-1", "CREDITO", 100m, Utilidades.Ontem, "venda");
        using var segunda = await Cliente.LancarAsync(caixa, "chave-1", "CREDITO", 100m, Utilidades.Ontem, "venda");

        Assert.Equal(HttpStatusCode.Created, primeira.StatusCode);
        Assert.Equal(HttpStatusCode.OK, segunda.StatusCode);
        Assert.Equal("true", segunda.Headers.GetValues(EndpointsDeLancamentos.CabecalhoRepeticao).Single());

        var original = await primeira.Content.ReadFromJsonAsync<LancamentoRegistrado>(TestContext.Current.CancellationToken);
        var repetido = await segunda.Content.ReadFromJsonAsync<LancamentoRegistrado>(TestContext.Current.CancellationToken);
        Assert.Equal(original!.Id, repetido!.Id);
        Assert.Equal(1, await ContarAsync(caixa));
    }

    [Fact]
    public async Task Envios_simultaneos_com_a_mesma_chave_geram_um_lancamento_so()
    {
        var caixa = Utilidades.NovoCaixa();

        var respostas = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ =>
            Cliente.LancarAsync(caixa, "chave-concorrente", "DEBITO", 7.5m, Utilidades.Ontem)));

        Assert.Single(respostas, r => r.StatusCode == HttpStatusCode.Created);
        Assert.All(respostas, r => Assert.True(r.StatusCode is HttpStatusCode.Created or HttpStatusCode.OK));
        Assert.Equal(1, await ContarAsync(caixa));
        foreach (var r in respostas)
        {
            r.Dispose();
        }
    }

    [Fact]
    public async Task Mesma_chave_com_corpo_diferente_e_recusada_com_422()
    {
        var caixa = Utilidades.NovoCaixa();

        using var primeira = await Cliente.LancarAsync(caixa, "chave-2", "CREDITO", 100m, Utilidades.Ontem);
        using var diferente = await Cliente.LancarAsync(caixa, "chave-2", "CREDITO", 999m, Utilidades.Ontem);

        Assert.Equal(HttpStatusCode.Created, primeira.StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, diferente.StatusCode);
        Assert.Equal(1, await ContarAsync(caixa));
    }

    [Fact]
    public async Task Sem_chave_de_idempotencia_e_recusado_com_400()
    {
        using var resposta = await Cliente.PostAsJsonAsync(
            $"/v1/caixas/{Utilidades.NovoCaixa()}/lancamentos",
            new { tipo = "CREDITO", valor = 1m, dataDoFato = Utilidades.Ontem }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
    }

    [Fact]
    public async Task Data_futura_e_valor_negativo_sao_recusados_com_400()
    {
        var caixa = Utilidades.NovoCaixa();

        using var futura = await Cliente.LancarAsync(caixa, "f", "CREDITO", 1m, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2)));
        using var negativo = await Cliente.LancarAsync(caixa, "n", "DEBITO", -1m, Utilidades.Ontem);
        using var tipo = await Cliente.LancarAsync(caixa, "t", "PIX", 1m, Utilidades.Ontem);

        Assert.Equal(HttpStatusCode.BadRequest, futura.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, negativo.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, tipo.StatusCode);
        Assert.Equal(0, await ContarAsync(caixa));
    }

    [Fact]
    public async Task Estorno_valido_e_aceito_uma_vez_e_estorno_incoerente_e_recusado()
    {
        var caixa = Utilidades.NovoCaixa();
        using var venda = await Cliente.LancarAsync(caixa, "venda", "CREDITO", 80m, Utilidades.Ontem);
        var original = await venda.Content.ReadFromJsonAsync<LancamentoRegistrado>(TestContext.Current.CancellationToken);

        using var valorErrado = await Cliente.LancarAsync(caixa, "e0", "DEBITO", 79m, Utilidades.Ontem, estornoDe: original!.Id);
        using var estorno = await Cliente.LancarAsync(caixa, "e1", "DEBITO", 80m, Utilidades.Ontem, estornoDe: original.Id);
        using var segundoEstorno = await Cliente.LancarAsync(caixa, "e2", "DEBITO", 80m, Utilidades.Ontem, estornoDe: original.Id);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, valorErrado.StatusCode);
        Assert.Equal(HttpStatusCode.Created, estorno.StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, segundoEstorno.StatusCode);
    }

    [Fact]
    public async Task Lista_do_dia_traz_so_os_lancamentos_do_caixa_e_da_data()
    {
        var caixa = Utilidades.NovoCaixa();
        using var a = await Cliente.LancarAsync(caixa, "a", "CREDITO", 10m, Utilidades.Ontem);
        using var b = await Cliente.LancarAsync(caixa, "b", "DEBITO", 4m, Utilidades.Ontem);
        using var c = await Cliente.LancarAsync(caixa, "c", "CREDITO", 1m, Utilidades.Ontem.AddDays(-1));

        var lista = await Cliente.GetFromJsonAsync<List<LancamentoRegistrado>>(
            $"/v1/caixas/{caixa}/lancamentos?data={Utilidades.Ontem:yyyy-MM-dd}", TestContext.Current.CancellationToken);

        Assert.Equal(2, lista!.Count);
        Assert.All(lista, l => Assert.Equal(Utilidades.Ontem, l.DataDoFato));
    }

    private async Task<long> ContarAsync(string caixa)
    {
        await using var conexao = new NpgsqlConnection(banco.ConexaoAdmin);
        return await conexao.ExecuteScalarAsync<long>("SELECT count(*) FROM lancamentos WHERE caixa_id = @caixa", new { caixa });
    }
}
