using System.Data;
using Dapper;
using Npgsql;

namespace CaixaDiario.Consolidado.Api;

public sealed record SaldoDiario(
    string CaixaId,
    DateOnly Data,
    decimal TotalCreditos,
    decimal TotalDebitos,
    decimal SaldoDoDia,
    int Quantidade,
    DateTimeOffset? AtualizadoAte,
    bool Defasado);

/// <summary>
/// Modelo de leitura do consolidado. Tudo aqui e reconstruivel reprocessando
/// os eventos: a fonte da verdade e o registro em Lancamentos.
/// </summary>
public sealed class RepositorioConsolidado(NpgsqlDataSource dataSource)
{
    /// <summary>
    /// Aplica o evento uma unica vez (RF-10). A insercao em
    /// lancamentos_aplicados e a soma no saldo acontecem na mesma transacao;
    /// a chave primaria decide se e reentrega.
    /// </summary>
    /// <returns>true se aplicou; false se era reentrega.</returns>
    public async Task<bool> AplicarAsync(EventoLancamentoRegistradoV1 evento, CancellationToken cancellationToken)
    {
        await using var conexao = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var transacao = await conexao.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        var data = evento.DataDoFato.ToDateTime(TimeOnly.MinValue);
        var registradoEm = evento.RegistradoEm.UtcDateTime;

        var inseridos = await conexao.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO lancamentos_aplicados (lancamento_id, caixa_id, data_do_fato)
            VALUES (@LancamentoId, @CaixaId, @data)
            ON CONFLICT (lancamento_id) DO NOTHING
            """,
            new { evento.LancamentoId, evento.CaixaId, data = new DataParametro(data) },
            transacao, cancellationToken: cancellationToken)).ConfigureAwait(false);

        if (inseridos == 0)
        {
            await transacao.CommitAsync(cancellationToken).ConfigureAwait(false);
            return false;
        }

        var credito = evento.Tipo == EventoLancamentoRegistradoV1.Credito ? evento.Valor : 0m;
        var debito = evento.Tipo == EventoLancamentoRegistradoV1.Debito ? evento.Valor : 0m;

        await conexao.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO saldo_diario (caixa_id, data, total_creditos, total_debitos, quantidade, atualizado_ate)
            VALUES (@CaixaId, @data, @credito, @debito, 1, @registradoEm)
            ON CONFLICT (caixa_id, data) DO UPDATE SET
                total_creditos = saldo_diario.total_creditos + EXCLUDED.total_creditos,
                total_debitos  = saldo_diario.total_debitos + EXCLUDED.total_debitos,
                quantidade     = saldo_diario.quantidade + 1,
                atualizado_ate = GREATEST(saldo_diario.atualizado_ate, EXCLUDED.atualizado_ate)
            """,
            new { evento.CaixaId, data = new DataParametro(data), credito, debito, registradoEm },
            transacao, cancellationToken: cancellationToken)).ConfigureAwait(false);

        await conexao.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO estado_projecao (id, ultimo_registrado_em, ultimo_aplicado_em)
            VALUES (1, @registradoEm, now())
            ON CONFLICT (id) DO UPDATE SET
                ultimo_registrado_em = EXCLUDED.ultimo_registrado_em,
                ultimo_aplicado_em = EXCLUDED.ultimo_aplicado_em
            """,
            new { registradoEm }, transacao, cancellationToken: cancellationToken)).ConfigureAwait(false);

        await transacao.CommitAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    public async Task<SaldoDiario> ConsultarAsync(
        string caixaId, DateOnly data, Func<double?, bool> avaliarDefasagem, CancellationToken cancellationToken)
    {
        await using var conexao = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);

        var linha = await conexao.QuerySingleOrDefaultAsync<Linha>(new CommandDefinition(
            """
            SELECT total_creditos AS TotalCreditos, total_debitos AS TotalDebitos,
                   quantidade AS Quantidade, atualizado_ate AS AtualizadoAte
            FROM saldo_diario
            WHERE caixa_id = @caixaId AND data = @data
            """,
            new { caixaId, data = new DataParametro(data.ToDateTime(TimeOnly.MinValue)) },
            cancellationToken: cancellationToken)).ConfigureAwait(false);

        // Atraso do ultimo evento aplicado: tempo entre o registro em
        // Lancamentos e a aplicacao aqui. A resposta avisa em vez de falhar.
        var atrasoSegundos = await conexao.ExecuteScalarAsync<double?>(new CommandDefinition(
            "SELECT EXTRACT(EPOCH FROM (ultimo_aplicado_em - ultimo_registrado_em))::float8 FROM estado_projecao WHERE id = 1",
            cancellationToken: cancellationToken)).ConfigureAwait(false);
        var defasado = avaliarDefasagem(atrasoSegundos);

        if (linha is null)
        {
            return new SaldoDiario(caixaId, data, 0m, 0m, 0m, 0, null, defasado);
        }

        return new SaldoDiario(
            caixaId, data, linha.TotalCreditos, linha.TotalDebitos, linha.TotalCreditos - linha.TotalDebitos,
            linha.Quantidade, new DateTimeOffset(DateTime.SpecifyKind(linha.AtualizadoAte, DateTimeKind.Utc)), defasado);
    }

    private sealed class Linha
    {
        public decimal TotalCreditos { get; set; }

        public decimal TotalDebitos { get; set; }

        public int Quantidade { get; set; }

        public DateTime AtualizadoAte { get; set; }
    }

    /// <summary>Envia a data como coluna date, sem conversao para timestamp.</summary>
    private sealed class DataParametro(DateTime valor) : SqlMapper.ICustomQueryParameter
    {
        public void AddParameter(IDbCommand command, string name)
        {
            var parametro = new NpgsqlParameter(name, NpgsqlTypes.NpgsqlDbType.Date) { Value = valor };
            command.Parameters.Add(parametro);
        }
    }
}
