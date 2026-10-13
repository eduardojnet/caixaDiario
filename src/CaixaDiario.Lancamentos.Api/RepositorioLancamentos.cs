using System.Diagnostics;
using CaixaDiario.Lancamentos.Dominio;
using Dapper;
using Npgsql;

namespace CaixaDiario.Lancamentos.Api;

public enum SituacaoRegistro
{
    Criado,
    Repetido,
    ChaveComOutroConteudo,
    EstornoInvalido,
    JaEstornado,
}

public sealed record LancamentoRegistrado(
    Guid Id,
    string CaixaId,
    string Tipo,
    decimal Valor,
    DateOnly DataDoFato,
    string Descricao,
    Guid? EstornoDe,
    DateTimeOffset RegistradoEm);

public sealed record ResultadoRegistro(SituacaoRegistro Situacao, LancamentoRegistrado? Lancamento);

/// <summary>
/// Persistencia de Lancamentos. A idempotencia e decidida pela chave unica do
/// banco, nao por consulta previa: dois envios simultaneos com a mesma chave
/// nao conseguem criar dois lancamentos (RNF-07).
/// </summary>
public sealed class RepositorioLancamentos(NpgsqlDataSource dataSource)
{
    private const string ViolacaoDeUnicidade = "23505";
    private const string RestricaoIdempotencia = "uq_lancamentos_idempotencia";
    private const string RestricaoEstorno = "uq_lancamentos_estorno";

    private const string Colunas = """
        id AS Id, caixa_id AS CaixaId, tipo AS Tipo, valor AS Valor,
        data_do_fato AS DataDoFato, descricao AS Descricao, estorno_de AS EstornoDe,
        registrado_em AS RegistradoEm
        """;

    public async Task<ResultadoRegistro> RegistrarAsync(
        Lancamento lancamento, string chaveIdempotencia, string hashRequisicao, CancellationToken cancellationToken)
    {
        await using var conexao = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var transacao = await conexao.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            if (lancamento.EstornoDe is { } originalId)
            {
                var original = await conexao.QuerySingleOrDefaultAsync<Linha>(
                    new CommandDefinition(
                        $"SELECT {Colunas} FROM lancamentos WHERE id = @originalId",
                        new { originalId }, transacao, cancellationToken: cancellationToken)).ConfigureAwait(false);

                if (original is null
                    || !TipoLancamentoTexto.TentarLer(original.Tipo, out var tipoOriginal)
                    || !lancamento.EstornaCorretamente(original.CaixaId, tipoOriginal, original.Valor))
                {
                    await transacao.RollbackAsync(cancellationToken).ConfigureAwait(false);
                    return new ResultadoRegistro(SituacaoRegistro.EstornoInvalido, null);
                }
            }

            var registrado = (await conexao.QuerySingleAsync<Linha>(new CommandDefinition(
                $"""
                INSERT INTO lancamentos
                    (id, caixa_id, tipo, valor, data_do_fato, descricao, estorno_de, chave_idempotencia, hash_requisicao)
                VALUES
                    (@Id, @CaixaId, @Tipo, @Valor, @DataDoFato, @Descricao, @EstornoDe, @Chave, @Hash)
                RETURNING {Colunas}
                """,
                new
                {
                    lancamento.Id,
                    lancamento.CaixaId,
                    Tipo = lancamento.Tipo.ParaTexto(),
                    lancamento.Valor,
                    lancamento.DataDoFato,
                    lancamento.Descricao,
                    lancamento.EstornoDe,
                    Chave = chaveIdempotencia,
                    Hash = hashRequisicao,
                },
                transacao, cancellationToken: cancellationToken)).ConfigureAwait(false)).ParaRegistrado();

            // Mesma transacao do lancamento: nao existe lancamento sem evento
            // nem evento sem lancamento (outbox transacional, ADR-0002).
            var evento = new EventoLancamentoRegistradoV1(
                Guid.CreateVersion7(), 1, registrado.Id, registrado.CaixaId, registrado.Tipo,
                registrado.Valor, registrado.DataDoFato, registrado.RegistradoEm);

            await conexao.ExecuteAsync(new CommandDefinition(
                "INSERT INTO outbox (evento_id, tipo_evento, payload, traceparent) VALUES (@EventoId, @TipoEvento, @Payload::jsonb, @Traceparent)",
                new
                {
                    evento.EventoId,
                    TipoEvento = EventoLancamentoRegistradoV1.NomeDoTipo,
                    Payload = evento.Serializar(),
                    // Contexto da requisicao: o despachante continua este traco ao publicar.
                    Traceparent = Activity.Current?.Id,
                },
                transacao, cancellationToken: cancellationToken)).ConfigureAwait(false);

            await transacao.CommitAsync(cancellationToken).ConfigureAwait(false);
            return new ResultadoRegistro(SituacaoRegistro.Criado, registrado);
        }
        catch (PostgresException ex) when (ex.SqlState == ViolacaoDeUnicidade && ex.ConstraintName == RestricaoIdempotencia)
        {
            await transacao.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return await ResolverRepeticaoAsync(lancamento.CaixaId, chaveIdempotencia, hashRequisicao, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (PostgresException ex) when (ex.SqlState == ViolacaoDeUnicidade && ex.ConstraintName == RestricaoEstorno)
        {
            await transacao.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return new ResultadoRegistro(SituacaoRegistro.JaEstornado, null);
        }
    }

    public async Task<IReadOnlyList<LancamentoRegistrado>> ListarDoDiaAsync(
        string caixaId, DateOnly data, CancellationToken cancellationToken)
    {
        await using var conexao = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var linhas = await conexao.QueryAsync<Linha>(new CommandDefinition(
            $"""
            SELECT {Colunas} FROM lancamentos
            WHERE caixa_id = @caixaId AND data_do_fato = @data
            ORDER BY registrado_em, id
            """,
            new { caixaId, data }, cancellationToken: cancellationToken)).ConfigureAwait(false);
        return [.. linhas.Select(l => l.ParaRegistrado())];
    }

    private async Task<ResultadoRegistro> ResolverRepeticaoAsync(
        string caixaId, string chave, string hash, CancellationToken cancellationToken)
    {
        await using var conexao = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var original = await conexao.QuerySingleAsync<Linha>(new CommandDefinition(
            $"SELECT {Colunas}, hash_requisicao AS HashRequisicao FROM lancamentos WHERE caixa_id = @caixaId AND chave_idempotencia = @chave",
            new { caixaId, chave }, cancellationToken: cancellationToken)).ConfigureAwait(false);

        return string.Equals(original.HashRequisicao, hash, StringComparison.Ordinal)
            ? new ResultadoRegistro(SituacaoRegistro.Repetido, original.ParaRegistrado())
            : new ResultadoRegistro(SituacaoRegistro.ChaveComOutroConteudo, null);
    }

    // Linha crua do banco. O Npgsql entrega date e timestamptz como DateTime;
    // a conversao para os tipos do contrato fica aqui.
    private sealed class Linha
    {
        public Guid Id { get; set; }

        public string CaixaId { get; set; } = string.Empty;

        public string Tipo { get; set; } = string.Empty;

        public decimal Valor { get; set; }

        public DateTime DataDoFato { get; set; }

        public string Descricao { get; set; } = string.Empty;

        public Guid? EstornoDe { get; set; }

        public DateTime RegistradoEm { get; set; }

        public string? HashRequisicao { get; set; }

        public LancamentoRegistrado ParaRegistrado() => new(
            Id, CaixaId, Tipo, Valor, DateOnly.FromDateTime(DataDoFato), Descricao, EstornoDe,
            new DateTimeOffset(DateTime.SpecifyKind(RegistradoEm, DateTimeKind.Utc)));
    }
}
