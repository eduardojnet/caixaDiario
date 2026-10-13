using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace CaixaDiario.Consolidado.Api;

/// <summary>
/// Fonte de tracos e medidor do Consolidado (ADR-0003): defasagem do
/// projetor (RNF-05), fila pendente, conexao com o broker, eventos aplicados e
/// reentregas descartadas.
/// </summary>
public sealed class Telemetria
{
    public const string Nome = "CaixaDiario.Consolidado";

    public static readonly ActivitySource Fonte = new(Nome);

    public Telemetria(IMeterFactory fabrica, EstadoDoProjetor estado)
    {
        var medidor = fabrica.Create(Nome);
        Aplicados = medidor.CreateCounter<long>(
            "caixadiario.projetor.aplicados", description: "Lancamentos aplicados ao saldo");
        Reentregas = medidor.CreateCounter<long>(
            "caixadiario.projetor.reentregas", description: "Eventos repetidos descartados pela chave do lancamento");
        medidor.CreateObservableGauge(
            "caixadiario.projetor.defasagem", () => estado.DefasagemDoUltimoEventoSegundos,
            unit: "s", description: "Tempo entre o registro em Lancamentos e a aplicacao do ultimo evento (RNF-05)");
        medidor.CreateObservableGauge(
            "caixadiario.projetor.fila_pendente", () => estado.Pendentes,
            description: "Mensagens esperando na fila do Consolidado");
        medidor.CreateObservableGauge(
            "caixadiario.projetor.idade_do_batimento", () => estado.IdadeDoBatimento.TotalSeconds,
            unit: "s", description: "Tempo desde o ultimo batimento do despachante de Lancamentos");
        medidor.CreateObservableGauge(
            "caixadiario.projetor.outbox_pendentes_na_origem", () => estado.OutboxPendentesNaOrigem,
            description: "Eventos pendentes na outbox de Lancamentos, segundo o ultimo batimento");
        medidor.CreateObservableGauge(
            "caixadiario.projetor.conectado", () => estado.Conectado ? 1 : 0,
            description: "1 quando o projetor esta conectado ao broker");
    }

    public Counter<long> Aplicados { get; }

    public Counter<long> Reentregas { get; }
}
