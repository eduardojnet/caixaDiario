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

    /// <summary>Criterio do cartao 42: reconciliacao do saldo acumulado entre dias.</summary>
    [Fact]
    public async Task Saldo_acumulado_reconcilia_entre_dias_inclusive_com_retroativo()
    {
        var repositorio = new RepositorioConsolidado(_dataSource);
        var caixa = Utilidades.NovoCaixa();
        var d1 = Utilidades.Ontem.AddDays(-2);
        var d2 = Utilidades.Ontem.AddDays(-1);
        var d3 = Utilidades.Ontem;

        await repositorio.AplicarAsync(Evento(caixa, "CREDITO", 100m, d1), CancellationToken.None);
        await repositorio.AplicarAsync(Evento(caixa, "DEBITO", 30m, d2), CancellationToken.None);
        await repositorio.AplicarAsync(Evento(caixa, "CREDITO", 5.50m, d3), CancellationToken.None);
        await repositorio.AplicarAsync(Evento("outro-caixa-acumulado", "CREDITO", 999m, d1), CancellationToken.None);

        async Task<SaldoDiario> Saldo(DateOnly d) =>
            await repositorio.ConsultarAsync(caixa, d, _ => false, CancellationToken.None);

        Assert.Equal(100m, (await Saldo(d1)).SaldoAcumulado);
        Assert.Equal(70m, (await Saldo(d2)).SaldoAcumulado);
        Assert.Equal(75.50m, (await Saldo(d3)).SaldoAcumulado);
        Assert.Equal(5.50m, (await Saldo(d3)).SaldoDoDia);

        // Acumulado do dia N = acumulado do dia N-1 + resultado do dia N.
        Assert.Equal((await Saldo(d2)).SaldoAcumulado + (await Saldo(d3)).SaldoDoDia, (await Saldo(d3)).SaldoAcumulado);

        // Retroativo (QA-03): um debito no primeiro dia muda o acumulado de todos os seguintes.
        await repositorio.AplicarAsync(Evento(caixa, "DEBITO", 20m, d1), CancellationToken.None);
        Assert.Equal(80m, (await Saldo(d1)).SaldoAcumulado);
        Assert.Equal(55.50m, (await Saldo(d3)).SaldoAcumulado);

        // Dia sem lancamento depois do ultimo: carrega o acumulado.
        Assert.Equal(55.50m, (await Saldo(d3.AddDays(1))).SaldoAcumulado);
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
        var relogio = new RelogioDeTeste();
        var estado = new EstadoDoProjetor(relogio);
        var limite = TimeSpan.FromSeconds(5);
        var limiteDoBatimento = TimeSpan.FromSeconds(15);

        Assert.True(estado.Defasado(0, limite, limiteDoBatimento));

        estado.Conectou();
        Assert.False(estado.Defasado(30, limite, limiteDoBatimento));

        estado.AtualizarPendentes(10);
        Assert.False(estado.Defasado(1, limite, limiteDoBatimento));
        Assert.True(estado.Defasado(30, limite, limiteDoBatimento));
    }

    /// <summary>Criterio do cartao 43: sem batimento, ou com outbox atrasada na origem, o saldo sai defasado.</summary>
    [Fact]
    public void Estado_do_projetor_marca_defasado_quando_o_batimento_atrasa_ou_a_outbox_acumula()
    {
        var relogio = new RelogioDeTeste();
        var estado = new EstadoDoProjetor(relogio);
        var limite = TimeSpan.FromSeconds(5);
        var limiteDoBatimento = TimeSpan.FromSeconds(15);
        estado.Conectou();

        // Carencia ao conectar e batimentos em dia: nao defasado.
        relogio.Avancar(TimeSpan.FromSeconds(10));
        Assert.False(estado.Defasado(null, limite, limiteDoBatimento));
        estado.RegistrarBatimento(0, 0);
        relogio.Avancar(TimeSpan.FromSeconds(14));
        Assert.False(estado.Defasado(null, limite, limiteDoBatimento));

        // Batimento atrasou: a publicacao em Lancamentos parou.
        relogio.Avancar(TimeSpan.FromSeconds(2));
        Assert.True(estado.Defasado(null, limite, limiteDoBatimento));

        // Batimento volta, mas informa evento pendente na origem mais velho que o limite.
        estado.RegistrarBatimento(7, 12.5);
        Assert.True(estado.Defasado(null, limite, limiteDoBatimento));
        Assert.Equal(7, estado.OutboxPendentesNaOrigem);

        // Origem em dia de novo.
        estado.RegistrarBatimento(0, 0);
        Assert.False(estado.Defasado(null, limite, limiteDoBatimento));
    }

    private sealed class RelogioDeTeste : TimeProvider
    {
        private DateTimeOffset _agora = new(2026, 10, 12, 12, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => _agora;

        public void Avancar(TimeSpan intervalo) => _agora += intervalo;
    }

    public async ValueTask DisposeAsync() => await _dataSource.DisposeAsync();
}
