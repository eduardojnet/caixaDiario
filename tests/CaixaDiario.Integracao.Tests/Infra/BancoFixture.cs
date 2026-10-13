using Npgsql;
using Testcontainers.PostgreSql;

[assembly: AssemblyFixture(typeof(CaixaDiario.Integracao.Tests.Infra.BancoLancamentosFixture))]
[assembly: AssemblyFixture(typeof(CaixaDiario.Integracao.Tests.Infra.BancoConsolidadoFixture))]

namespace CaixaDiario.Integracao.Tests.Infra;

/// <summary>
/// PostgreSQL real por execucao, com o MESMO script que o compose aplica
/// (regra 8). Banco em memoria passaria na implementacao ingenua: nao tem
/// chave unica concorrente, nem papel, nem privilegio.
/// </summary>
public abstract class BancoFixture(string banco, string pastaSql, string papelAplicacao) : IAsyncLifetime
{
    private const string SenhaAplicacao = "senha_app_teste";

    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase(banco)
        .WithUsername($"{banco}_admin")
        .WithPassword("senha_admin_teste")
        .Build();

    private string? _conexaoAplicacao;

    /// <summary>Conexao com o papel da aplicacao, o mesmo privilegio do compose.</summary>
    public string ConexaoAplicacao =>
        _conexaoAplicacao ?? throw new InvalidOperationException("Fixture nao inicializada.");

    /// <summary>Conexao administrativa, so para inspecao nos testes.</summary>
    public string ConexaoAdmin => _container.GetConnectionString();

    public async ValueTask InitializeAsync()
    {
        await _container.StartAsync().ConfigureAwait(false);

        // Todos os .sql da pasta do servico, em ordem de nome, como o initdb do compose.
        var scripts = Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "db", pastaSql), "*.sql")
            .Order(StringComparer.Ordinal);

        await using (var conexao = new NpgsqlConnection(ConexaoAdmin))
        {
            await conexao.OpenAsync().ConfigureAwait(false);
            foreach (var arquivo in scripts)
            {
                var script = await File.ReadAllTextAsync(arquivo).ConfigureAwait(false);
                await using var esquema = new NpgsqlCommand(script, conexao);
                await esquema.ExecuteNonQueryAsync().ConfigureAwait(false);
            }

            // Equivalente ao 0002_senha_aplicacao.sh do compose.
            await using var senha = new NpgsqlCommand(
                $"ALTER ROLE {papelAplicacao} LOGIN PASSWORD '{SenhaAplicacao}'", conexao);
            await senha.ExecuteNonQueryAsync().ConfigureAwait(false);
        }

        _conexaoAplicacao = new NpgsqlConnectionStringBuilder(ConexaoAdmin)
        {
            Username = papelAplicacao,
            Password = SenhaAplicacao,
        }.ConnectionString;
    }

    public async ValueTask DisposeAsync()
    {
        await _container.DisposeAsync().ConfigureAwait(false);
        GC.SuppressFinalize(this);
    }
}

public sealed class BancoLancamentosFixture()
    : BancoFixture("lancamentos", "lancamentos", "lancamentos_app");

/// <summary>
/// Banco exclusivo do teste de RNF-01: ele conta a outbox, e outro despachante
/// ligado a um broker esvaziaria a outbox compartilhada no meio da contagem.
/// </summary>
public sealed class BancoLancamentosIsoladoFixture()
    : BancoFixture("lancamentos", "lancamentos", "lancamentos_app");

public sealed class BancoConsolidadoFixture()
    : BancoFixture("consolidado", "consolidado", "consolidado_app");
