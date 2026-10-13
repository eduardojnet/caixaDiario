using DbUp;
using Npgsql;

namespace CaixaDiario.Lancamentos.Migracoes;

/// <summary>Resultado de uma execucao: os scripts aplicados agora, ou o erro.</summary>
public sealed record ResultadoMigracao(bool Sucesso, IReadOnlyList<string> Aplicados, Exception? Erro);

/// <summary>
/// Aplica os scripts versionados de db/lancamentos/ que ainda nao rodaram neste banco.
/// O DbUp registra cada script num diario; a execucao seguinte so aplica o que
/// falta. Mudanca de esquema e um script novo, sem recriar o volume.
/// </summary>
/// <remarks>
/// Roda com a credencial administrativa, num passo que termina antes de a API
/// subir. A API continua com o papel lancamentos_app, sem UPDATE nem DELETE no
/// que e imutavel: a credencial capaz de alterar o esquema nunca fica com o
/// processo em execucao.
/// </remarks>
public static class Migrador
{
    public const string PapelDaAplicacao = "lancamentos_app";
    public const string Diario = "versoes_do_esquema";

    public static string PastaPadrao => Path.Combine(AppContext.BaseDirectory, "scripts", "lancamentos");

    public static ResultadoMigracao Aplicar(string conexaoAdmin, string? pasta = null, bool registrarNoConsole = false)
    {
        var construtor = DeployChanges.To
            .PostgresqlDatabase(conexaoAdmin)
            .WithScriptsFromFileSystem(pasta ?? PastaPadrao)
            .JournalToPostgresqlTable("public", Diario)
            // Script com erro desfaz o que fez e nao entra no diario.
            .WithTransactionPerScript()
            // $nome$ e delimitador de bloco no PostgreSQL, nao variavel do DbUp.
            .WithVariablesDisabled();

        construtor = registrarNoConsole ? construtor.LogToConsole() : construtor.LogToNowhere();
        var resultado = construtor.Build().PerformUpgrade();

        return new ResultadoMigracao(
            resultado.Successful, [.. resultado.Scripts.Select(s => s.Name)], resultado.Error);
    }

    /// <summary>
    /// Habilita o login do papel da aplicacao com a senha recebida por variavel
    /// de ambiente. Roda a cada execucao: trocar a senha no .env e subir de novo
    /// basta. O literal e montado pelo proprio PostgreSQL (format %L).
    /// </summary>
    public static void DefinirSenhaDaAplicacao(string conexaoAdmin, string senha)
    {
        using var conexao = new NpgsqlConnection(conexaoAdmin);
        conexao.Open();

        using var montar = new NpgsqlCommand("SELECT format('ALTER ROLE %I LOGIN PASSWORD %L', @papel, @senha)", conexao);
        montar.Parameters.AddWithValue("papel", PapelDaAplicacao);
        montar.Parameters.AddWithValue("senha", senha);
        var comando = (string)montar.ExecuteScalar()!;

        using var alterar = new NpgsqlCommand(comando, conexao);
        alterar.ExecuteNonQuery();
    }
}
