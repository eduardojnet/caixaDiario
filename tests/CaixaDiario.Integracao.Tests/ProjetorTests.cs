using CaixaDiario.Consolidado.Api;
using CaixaDiario.Integracao.Tests.Infra;
using Npgsql;

namespace CaixaDiario.Integracao.Tests;

/// <summary>RF-10: o projetor aplica cada lancamento uma vez, contra banco real.</summary>
public sealed class ProjetorTests(BancoConsolidadoFixture banco) : IAsyncDisposable
{
    private readonly NpgsqlDataSource _dataSource = NpgsqlDataSource.Create(banco.ConexaoAplicacao);

    private static EventoLancamentoRegistradoV1 Evento(string caixa, string tipo, decimal valor, DateOnly data) =>
        new(Guid.NewGuid(), 1, Guid.NewGuid(), caixa, tipo, valor, data, DateTimeOffset.UtcNow);

    [Fact]
    public async Task Evento_entregue_duas_vezes_soma_uma_vez()
    {
        var repositorio = new RepositorioConsolidado(_dataSource);
        var caixa = Utilidades.NovoCaixa();
        var evento = Evento(caixa, "CREDITO", 42.10m, Utilidades.Ontem);

        var primeira = await repositorio.AplicarAsync(evento, CancellationToken.None);
        var reentrega = await repositorio.AplicarAsync(evento with { EventoId = Guid.NewGuid() }, CancellationToken.None);
        var saldo = await repositorio.ConsultarAsync(caixa, Utilidades.Ontem, _ => false, CancellationToken.None);

        Assert.True(primeira);
        Assert.False(reentrega);
        Assert.Equal(1, saldo.Quantidade);
        Assert.Equal(42.10m, saldo.TotalCreditos);
    }

    [Fact]
    public async Task Saldo_do_dia_e_creditos_menos_debitos_independente_da_ordem()
    {
        var repositorio = new RepositorioConsolidado(_dataSource);
        var caixa = Utilidades.NovoCaixa();
        var eventos = new[]
        {
            Evento(caixa, "DEBITO", 30m, Utilidades.Ontem),
            Evento(caixa, "CREDITO", 100m, Utilidades.Ontem),
            Evento(caixa, "DEBITO", 80.55m, Utilidades.Ontem),
            Evento(caixa, "CREDITO", 5m, Utilidades.Ontem.AddDays(-1)),
        };

        foreach (var evento in eventos)
        {
            await repositorio.AplicarAsync(evento, CancellationToken.None);
        }

        var saldo = await repositorio.ConsultarAsync(caixa, Utilidades.Ontem, _ => false, CancellationToken.None);

        Assert.Equal(100m, saldo.TotalCreditos);
        Assert.Equal(110.55m, saldo.TotalDebitos);
        Assert.Equal(-10.55m, saldo.SaldoDoDia);
        Assert.Equal(3, saldo.Quantidade);
    }

    [Fact]
    public async Task Dia_sem_lancamento_responde_zero()
    {
        var repositorio = new RepositorioConsolidado(_dataSource);

        var saldo = await repositorio.ConsultarAsync(Utilidades.NovoCaixa(), Utilidades.Ontem, _ => false, CancellationToken.None);

        Assert.Equal(0, saldo.Quantidade);
        Assert.Equal(0m, saldo.SaldoDoDia);
        Assert.Null(saldo.AtualizadoAte);
    }

    [Fact]
    public void Estado_do_projetor_marca_defasado_sem_broker_ou_com_fila_atrasada()
    {
        var estado = new EstadoDoProjetor();
        var limite = TimeSpan.FromSeconds(5);

        Assert.True(estado.Defasado(0, limite));

        estado.Conectou();
        Assert.False(estado.Defasado(30, limite));

        estado.AtualizarPendentes(10);
        Assert.False(estado.Defasado(1, limite));
        Assert.True(estado.Defasado(30, limite));
    }

    public async ValueTask DisposeAsync() => await _dataSource.DisposeAsync();
}
