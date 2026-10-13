using Dapper;
using Npgsql;
using Testcontainers.PostgreSql;
using MigradorConsolidado = CaixaDiario.Consolidado.Migracoes.Migrador;
using MigradorLancamentos = CaixaDiario.Lancamentos.Migracoes.Migrador;

namespace CaixaDiario.Integracao.Tests;

/// <summary>
/// Criterio do cartao 39: um migrador por servico; rodar de novo sobre o mesmo
/// banco nao reaplica nada; mudanca de esquema e um script novo, aplicado sem
/// recriar o banco.
/// </summary>
public sealed class MigracaoTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _lancamentos = new PostgreSqlBuilder("postgres:17-alpine").Build();
    private readonly PostgreSqlContainer _consolidado = new PostgreSqlBuilder("postgres:17-alpine").Build();

    public async ValueTask InitializeAsync() => await Task.WhenAll(
        _lancamentos.StartAsync(TestContext.Current.CancellationToken),
        _consolidado.StartAsync(TestContext.Current.CancellationToken));

    public async ValueTask DisposeAsync()
    {
        await _lancamentos.DisposeAsync();
        await _consolidado.DisposeAsync();
    }

    [Fact]
    public async Task Migrador_de_Lancamentos_aplica_uma_vez_e_depois_nao_reaplica()
    {
        var conexao = _lancamentos.GetConnectionString();

        var primeira = MigradorLancamentos.Aplicar(conexao);
        var segunda = MigradorLancamentos.Aplicar(conexao);

        Assert.True(primeira.Sucesso, primeira.Erro?.Message);
        Assert.Collection(primeira.Aplicados,
            s => Assert.EndsWith("0001_esquema.sql", s, StringComparison.Ordinal),
            s => Assert.EndsWith("0003_rastreamento.sql", s, StringComparison.Ordinal));
        Assert.True(segunda.Sucesso, segunda.Erro?.Message);
        Assert.Empty(segunda.Aplicados);
        Assert.Equal(2, await ContarDiarioAsync(conexao));
    }

    [Fact]
    public void Migrador_do_Consolidado_aplica_uma_vez_e_depois_nao_reaplica()
    {
        var conexao = _consolidado.GetConnectionString();

        var primeira = MigradorConsolidado.Aplicar(conexao);
        var segunda = MigradorConsolidado.Aplicar(conexao);

        Assert.True(primeira.Sucesso, primeira.Erro?.Message);
        Assert.Single(primeira.Aplicados);
        Assert.Empty(segunda.Aplicados);
    }

    [Fact]
    public async Task Script_novo_e_aplicado_sozinho_sobre_banco_existente()
    {
        var conexao = _lancamentos.GetConnectionString();
        Assert.True(MigradorLancamentos.Aplicar(conexao).Sucesso);

        // Mesma pasta de scripts, mais uma mudanca de esquema.
        var pasta = Directory.CreateTempSubdirectory("migracoes-").FullName;
        foreach (var arquivo in Directory.GetFiles(MigradorLancamentos.PastaPadrao, "*.sql"))
        {
            File.Copy(arquivo, Path.Combine(pasta, Path.GetFileName(arquivo)));
        }

        await File.WriteAllTextAsync(Path.Combine(pasta, "0004_teste.sql"),
            "ALTER TABLE outbox ADD COLUMN coluna_de_teste int NULL;", TestContext.Current.CancellationToken);

        var resultado = MigradorLancamentos.Aplicar(conexao, pasta);

        Assert.True(resultado.Sucesso, resultado.Erro?.Message);
        Assert.Collection(resultado.Aplicados, s => Assert.EndsWith("0004_teste.sql", s, StringComparison.Ordinal));
        Directory.Delete(pasta, recursive: true);
    }

    [Fact]
    public async Task Senha_do_papel_da_aplicacao_e_redefinida_a_cada_execucao()
    {
        var conexao = _consolidado.GetConnectionString();
        Assert.True(MigradorConsolidado.Aplicar(conexao).Sucesso);

        MigradorConsolidado.DefinirSenhaDaAplicacao(conexao, "primeira'senha");
        MigradorConsolidado.DefinirSenhaDaAplicacao(conexao, "segunda'senha");

        var comoAplicacao = new NpgsqlConnectionStringBuilder(conexao)
        {
            Username = MigradorConsolidado.PapelDaAplicacao,
            Password = "segunda'senha",
        }.ConnectionString;
        await using var aplicacao = new NpgsqlConnection(comoAplicacao);
        Assert.Equal(0, await aplicacao.ExecuteScalarAsync<int>("SELECT count(*) FROM saldo_diario"));
    }

    private static async Task<long> ContarDiarioAsync(string conexao)
    {
        await using var c = new NpgsqlConnection(conexao);
        return await c.ExecuteScalarAsync<long>($"SELECT count(*) FROM public.{MigradorLancamentos.Diario}");
    }
}
