using System.Reflection;
using CaixaDiario.Consolidado.Api;
using CaixaDiario.Lancamentos.Api;
using CaixaDiario.Lancamentos.Dominio;
using NetArchTest.Rules;
using TestResult = NetArchTest.Rules.TestResult;

namespace CaixaDiario.Arquitetura.Tests;

/// <summary>
/// Regra 1 e RNF-01 em forma executavel: o Controle de Lancamentos nao conhece
/// o Consolidado Diario. Uma fronteira que so existe no diagrama dura ate a
/// primeira entrega apressada; aqui ela reprova os testes.
/// </summary>
public class RegrasDeDependenciaTests
{
    private static readonly Assembly LancamentosDominio = typeof(Lancamento).Assembly;
    private static readonly Assembly LancamentosApi = typeof(MarcadorLancamentosApi).Assembly;
    private static readonly Assembly ConsolidadoApi = typeof(MarcadorConsolidadoApi).Assembly;

    private static void Aprovar(TestResult resultado, string regra) =>
        Assert.True(
            resultado.IsSuccessful,
            $"{regra}. Tipos que violam: {string.Join(", ", resultado.FailingTypeNames ?? [])}");

    [Fact]
    public void Nenhum_tipo_de_Lancamentos_referencia_Consolidado()
    {
        foreach (var assembly in new[] { LancamentosDominio, LancamentosApi })
        {
            var resultado = Types.InAssembly(assembly)
                .ShouldNot()
                .HaveDependencyOn("CaixaDiario.Consolidado")
                .GetResult();

            Aprovar(resultado, $"{assembly.GetName().Name} não pode depender de CaixaDiario.Consolidado (regra 1, RNF-01)");
        }
    }

    [Fact]
    public void Assemblies_de_Lancamentos_nao_referenciam_assembly_do_Consolidado()
    {
        foreach (var assembly in new[] { LancamentosDominio, LancamentosApi })
        {
            var referencias = assembly.GetReferencedAssemblies().Select(r => r.Name);

            Assert.DoesNotContain(referencias,
                nome => nome is not null && nome.StartsWith("CaixaDiario.Consolidado", StringComparison.Ordinal));
        }
    }

    [Fact]
    public void Consolidado_nao_compartilha_assembly_com_Lancamentos()
    {
        // Sem assembly compartilhado (ADR-0002): o Consolidado conhece
        // Lancamentos so pelo contrato JSON do evento.
        var resultado = Types.InAssembly(ConsolidadoApi)
            .ShouldNot()
            .HaveDependencyOn("CaixaDiario.Lancamentos")
            .GetResult();

        Aprovar(resultado, "CaixaDiario.Consolidado.Api não pode depender de tipos de CaixaDiario.Lancamentos");
    }

    [Fact]
    public void Dominio_nao_conhece_banco_http_nem_broker()
    {
        var resultado = Types.InAssembly(LancamentosDominio)
            .ShouldNot()
            .HaveDependencyOnAny("Npgsql", "Dapper", "Microsoft.AspNetCore", "RabbitMQ", "CaixaDiario.Lancamentos.Api")
            .GetResult();

        Aprovar(resultado, "O domínio de Lançamentos não pode depender de infraestrutura nem da API");
    }
}
