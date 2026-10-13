namespace CaixaDiario.Consolidado.Api;

/// <summary>
/// O que o projetor sabe sobre o proprio atraso, sem perguntar nada a
/// Lancamentos: se esta conectado ao broker e quantas mensagens esperam na fila.
/// </summary>
public sealed class EstadoDoProjetor
{
    private int _conectado;
    private long _pendentes;
    private long _defasagemMilissegundos;

    public bool Conectado => Volatile.Read(ref _conectado) == 1;

    public long Pendentes => Interlocked.Read(ref _pendentes);

    public double DefasagemDoUltimoEventoSegundos => Interlocked.Read(ref _defasagemMilissegundos) / 1000.0;

    public void RegistrarDefasagem(TimeSpan defasagem) =>
        Interlocked.Exchange(ref _defasagemMilissegundos, (long)defasagem.TotalMilliseconds);

    public void Conectou() => Volatile.Write(ref _conectado, 1);

    public void Desconectou() => Volatile.Write(ref _conectado, 0);

    public void AtualizarPendentes(long pendentes) => Interlocked.Exchange(ref _pendentes, pendentes);

    /// <summary>
    /// RNF-05: defasado quando o projetor nao alcanca o broker (nao ha como
    /// garantir que o saldo esta em dia) ou quando ha fila acumulada e o ultimo
    /// evento aplicado chegou com atraso acima do limite.
    /// </summary>
    public bool Defasado(double? atrasoDoUltimoEventoSegundos, TimeSpan limite) =>
        !Conectado || (Pendentes > 0 && atrasoDoUltimoEventoSegundos is { } atraso && atraso > limite.TotalSeconds);
}
