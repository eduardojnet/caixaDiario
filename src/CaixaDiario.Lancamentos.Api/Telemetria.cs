using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace CaixaDiario.Lancamentos.Api;

/// <summary>
/// Fonte de tracos e medidor do servico (ADR-0003). As metricas respondem aos
/// RNF: lancamentos registrados, eventos publicados, e o tamanho e a idade da
/// outbox, que crescem quando o broker esta fora.
/// </summary>
public sealed class Telemetria
{
    public const string Nome = "CaixaDiario.Lancamentos";

    public static readonly ActivitySource Fonte = new(Nome);

    private long _outboxPendentes;
    private long _outboxIdadeMilissegundos;

    public Telemetria(IMeterFactory fabrica)
    {
        var medidor = fabrica.Create(Nome);
        Registrados = medidor.CreateCounter<long>(
            "caixadiario.lancamentos.registrados", description: "Lancamentos novos registrados, por tipo");
        Publicados = medidor.CreateCounter<long>(
            "caixadiario.outbox.publicados", description: "Eventos publicados no broker pelo despachante");
        medidor.CreateObservableGauge(
            "caixadiario.outbox.pendentes", () => Interlocked.Read(ref _outboxPendentes),
            description: "Eventos na outbox ainda nao publicados");
        medidor.CreateObservableGauge(
            "caixadiario.outbox.idade_mais_antiga", () => Interlocked.Read(ref _outboxIdadeMilissegundos) / 1000.0,
            unit: "s", description: "Idade do evento pendente mais antigo; cresce com o broker fora");
    }

    public Counter<long> Registrados { get; }

    public Counter<long> Publicados { get; }

    public void AtualizarOutbox(long pendentes, double idadeSegundos)
    {
        Interlocked.Exchange(ref _outboxPendentes, pendentes);
        Interlocked.Exchange(ref _outboxIdadeMilissegundos, (long)(idadeSegundos * 1000));
    }
}
