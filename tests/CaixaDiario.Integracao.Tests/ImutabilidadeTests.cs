using CaixaDiario.Integracao.Tests.Infra;
using Dapper;
using Npgsql;

namespace CaixaDiario.Integracao.Tests;

/// <summary>
/// RNF-06 e regra 7: a imutabilidade e garantida pelo privilegio no banco. O
/// papel da aplicacao tenta alterar e apagar, e o PostgreSQL recusa.
/// </summary>
public sealed class ImutabilidadeTests(BancoLancamentosFixture banco)
{
    private const string PermissaoNegada = "42501";

    [Fact]
    public async Task Papel_da_aplicacao_nao_altera_nem_apaga_lancamento()
    {
        await using var conexao = new NpgsqlConnection(banco.ConexaoAplicacao);
        var id = Guid.NewGuid();
        await conexao.ExecuteAsync(
            """
            INSERT INTO lancamentos (id, caixa_id, tipo, valor, data_do_fato, chave_idempotencia, hash_requisicao)
            VALUES (@id, 'caixa-imutavel', 'CREDITO', 10, current_date - 1, @chave, repeat('0', 64))
            """,
            new { id, chave = id.ToString() });

        var alterar = await Assert.ThrowsAsync<PostgresException>(() =>
            conexao.ExecuteAsync("UPDATE lancamentos SET valor = 1 WHERE id = @id", new { id }));
        var apagar = await Assert.ThrowsAsync<PostgresException>(() =>
            conexao.ExecuteAsync("DELETE FROM lancamentos WHERE id = @id", new { id }));

        Assert.Equal(PermissaoNegada, alterar.SqlState);
        Assert.Equal(PermissaoNegada, apagar.SqlState);
    }

    [Fact]
    public async Task Papel_da_aplicacao_so_altera_a_marca_de_publicacao_da_outbox()
    {
        await using var conexao = new NpgsqlConnection(banco.ConexaoAplicacao);
        var eventoId = Guid.NewGuid();
        await conexao.ExecuteAsync(
            "INSERT INTO outbox (evento_id, tipo_evento, payload) VALUES (@eventoId, 'teste', '{}'::jsonb)",
            new { eventoId });

        var alterarPayload = await Assert.ThrowsAsync<PostgresException>(() =>
            conexao.ExecuteAsync("UPDATE outbox SET payload = '{\"x\":1}'::jsonb WHERE evento_id = @eventoId", new { eventoId }));
        var apagar = await Assert.ThrowsAsync<PostgresException>(() =>
            conexao.ExecuteAsync("DELETE FROM outbox WHERE evento_id = @eventoId", new { eventoId }));
        var marcados = await conexao.ExecuteAsync(
            "UPDATE outbox SET publicado_em = now() WHERE evento_id = @eventoId", new { eventoId });

        Assert.Equal(PermissaoNegada, alterarPayload.SqlState);
        Assert.Equal(PermissaoNegada, apagar.SqlState);
        Assert.Equal(1, marcados);
    }
}
