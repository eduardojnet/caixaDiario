using System.Net;
using System.Net.Http.Json;
using CaixaDiario.Consolidado.Api;
using CaixaDiario.Integracao.Tests.Infra;
using CaixaDiario.Lancamentos.Api;
using Testcontainers.RabbitMq;

namespace CaixaDiario.Integracao.Tests;

/// <summary>
/// RNF-04, reconciliacao ponta a ponta: lancamentos registrados em
/// Lancamentos, despachados pela outbox, projetados pelo Consolidado. O saldo
/// do dia tem de ser igual a creditos menos debitos da lista do dia.
/// </summary>
public sealed class FluxoCompletoTests(BancoLancamentosFixture bancoLancamentos, BancoConsolidadoFixture bancoConsolidado)
    : IAsyncLifetime
{
    private readonly RabbitMqContainer _broker = new RabbitMqBuilder("rabbitmq:4.1.8-management-alpine")
        .WithUsername("guest")
        .WithPassword("guest")
        .Build();

    public async ValueTask InitializeAsync() => await _broker.StartAsync(TestContext.Current.CancellationToken);

    public async ValueTask DisposeAsync() => await _broker.DisposeAsync();

    [Fact]
    public async Task Saldo_projetado_reconcilia_com_os_lancamentos_do_dia()
    {
        var broker = new Broker(_broker.Hostname, _broker.GetMappedPublicPort(5672), "guest", "guest");
        await using var consolidado = new FabricaConsolidado(bancoConsolidado.ConexaoAplicacao, broker);
        using var clienteConsolidado = consolidado.CreateClient();
        await using var lancamentos = new FabricaLancamentos(bancoLancamentos.ConexaoAplicacao, broker);
        using var clienteLancamentos = lancamentos.CreateClient();

        var caixa = Utilidades.NovoCaixa();
        var dia = Utilidades.Ontem;
        var aleatorio = new Random(20261012);
        var primeiroTipo = string.Empty;
        var primeiroValor = 0m;
        for (var i = 0; i < 30; i++)
        {
            var tipo = aleatorio.Next(2) == 0 ? "CREDITO" : "DEBITO";
            var valor = Math.Round((decimal)(aleatorio.NextDouble() * 500) + 0.01m, 2);
            if (i == 0)
            {
                (primeiroTipo, primeiroValor) = (tipo, valor);
            }

            using var resposta = await clienteLancamentos.LancarAsync(caixa, $"k-{i}", tipo, valor, dia);
            Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);
        }

        // Reenvio de um ja registrado: nao pode chegar duas vezes ao saldo.
        using (var repetido = await clienteLancamentos.LancarAsync(caixa, "k-0", primeiroTipo, primeiroValor, dia))
        {
            Assert.Equal(HttpStatusCode.OK, repetido.StatusCode);
        }

        var lista = await clienteLancamentos.GetFromJsonAsync<List<LancamentoRegistrado>>(
            $"/v1/caixas/{caixa}/lancamentos?data={dia:yyyy-MM-dd}", TestContext.Current.CancellationToken);
        var creditos = lista!.Where(l => l.Tipo == "CREDITO").Sum(l => l.Valor);
        var debitos = lista!.Where(l => l.Tipo == "DEBITO").Sum(l => l.Valor);

        Assert.NotNull(lista);
        SaldoDiario? saldo = null;
        await Utilidades.EsperarAsync(async () =>
        {
            saldo = await clienteConsolidado.GetFromJsonAsync<SaldoDiario>(
                $"/v1/caixas/{caixa}/consolidado/{dia:yyyy-MM-dd}", TestContext.Current.CancellationToken);
            return saldo is not null && saldo.Quantidade == lista.Count;
        }, TimeSpan.FromSeconds(60), "projecao alcancar os 30 lancamentos");

        Assert.NotNull(saldo);
        Assert.Equal(30, lista.Count);
        Assert.Equal(creditos, saldo.TotalCreditos);
        Assert.Equal(debitos, saldo.TotalDebitos);
        Assert.Equal(creditos - debitos, saldo.SaldoDoDia);
        Assert.NotNull(saldo.AtualizadoAte);
    }
}
