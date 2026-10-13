using Npgsql;
using Testcontainers.PostgreSql;

[assembly: AssemblyFixture(typeof(CaixaDiario.Integracao.Tests.Infra.BancoLancamentosFixture))]
[assembly: AssemblyFixture(typeof(CaixaDiario.Integracao.Tests.Infra.BancoConsolidadoFixture))]

namespace CaixaDiario.Integracao.Tests.Infra;

/// <summary>
/// PostgreSQL real por execucao, preparado pelo MESMO migrador do compose
/// (regra 8, cartao 39). Banco em memoria passaria na implementacao ingenua:
/// nao tem chave unica concorrente, nem papel, nem privilegio.
/// </summary>
public abstract class BancoFixture(string banco) : IAsyncLifetime
{
    protected const string SenhaAplicacao = "senha_app_teste";

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

    protected abstract string PapelDaAplicacao { get; }

    /// <summary>Executa o migrador do servico: esquema e senha do papel.</summary>
    protected abstract void Migrar();

    public async ValueTask InitializeAsync()
    {
        await _container.StartAsync().ConfigureAwait(false);
        Migrar();
        _conexaoAplicacao = new NpgsqlConnectionStringBuilder(ConexaoAdmin)
        {
            Username = PapelDaAplicacao,
            Password = SenhaAplicacao,
        }.ConnectionString;
    }

    public async ValueTask DisposeAsync()
    {
        await _container.DisposeAsync().ConfigureAwait(false);
        GC.SuppressFinalize(this);
    }
}

public class BancoLancamentosFixture() : BancoFixture("lancamentos")
{
    protected override string PapelDaAplicacao => Lancamentos.Migracoes.Migrador.PapelDaAplicacao;

    protected override void Migrar()
    {
        var resultado = Lancamentos.Migracoes.Migrador.Aplicar(ConexaoAdmin);
        if (!resultado.Sucesso)
        {
            throw new InvalidOperationException("Migracao de Lancamentos falhou.", resultado.Erro);
        }

        Lancamentos.Migracoes.Migrador.DefinirSenhaDaAplicacao(ConexaoAdmin, SenhaAplicacao);
    }
}

/// <summary>
/// Banco exclusivo do teste de RNF-01: ele conta a outbox, e outro despachante
/// ligado a um broker esvaziaria a outbox compartilhada no meio da contagem.
/// </summary>
public sealed class BancoLancamentosIsoladoFixture : BancoLancamentosFixture;

public sealed class BancoConsolidadoFixture() : BancoFixture("consolidado")
{
    protected override string PapelDaAplicacao => Consolidado.Migracoes.Migrador.PapelDaAplicacao;

    protected override void Migrar()
    {
        var resultado = Consolidado.Migracoes.Migrador.Aplicar(ConexaoAdmin);
        if (!resultado.Sucesso)
        {
            throw new InvalidOperationException("Migracao de Consolidado falhou.", resultado.Erro);
        }

        Consolidado.Migracoes.Migrador.DefinirSenhaDaAplicacao(ConexaoAdmin, SenhaAplicacao);
    }
}
