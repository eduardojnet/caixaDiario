namespace CaixaDiario.Consolidado.Api;

/// <summary>
/// O que o projetor sabe sobre o proprio atraso, sem perguntar nada a
/// Lancamentos: se esta conectado ao broker, quantas mensagens esperam na fila,
/// quando chegou o ultimo batimento do despachante e o que ele informou da
/// outbox (cartao 43).
/// </summary>
public sealed class EstadoDoProjetor(TimeProvider? relogio = null)
{
    private readonly TimeProvider _relogio = relogio ?? TimeProvider.System;
    private int _conectado;
    private long _pendentes;
    private long _defasagemMilissegundos;
    private long _ultimoBatimentoTicks;
    private long _outboxPendentesNaOrigem;
    private long _outboxIdadeNaOrigemMilissegundos;

    public bool Conectado => Volatile.Read(ref _conectado) == 1;

    public long Pendentes => Interlocked.Read(ref _pendentes);

    public double DefasagemDoUltimoEventoSegundos => Interlocked.Read(ref _defasagemMilissegundos) / 1000.0;

    public long OutboxPendentesNaOrigem => Interlocked.Read(ref _outboxPendentesNaOrigem);

    public double OutboxIdadeNaOrigemSegundos => Interlocked.Read(ref _outboxIdadeNaOrigemMilissegundos) / 1000.0;

    /// <summary>Tempo desde o ultimo batimento recebido, medido no relogio deste servico.</summary>
    public TimeSpan IdadeDoBatimento =>
        _relogio.GetUtcNow() - new DateTimeOffset(Interlocked.Read(ref _ultimoBatimentoTicks), TimeSpan.Zero);

    /// <summary>Ao conectar, concede um batimento de carencia: nao ha como ter recebido um antes.</summary>
    public void Conectou()
    {
        Interlocked.Exchange(ref _ultimoBatimentoTicks, _relogio.GetUtcNow().UtcTicks);
        Volatile.Write(ref _conectado, 1);
    }

    public void Desconectou() => Volatile.Write(ref _conectado, 0);

    public void AtualizarPendentes(long pendentes) => Interlocked.Exchange(ref _pendentes, pendentes);

    public void RegistrarDefasagem(TimeSpan defasagem) =>
        Interlocked.Exchange(ref _defasagemMilissegundos, (long)defasagem.TotalMilliseconds);

    public void RegistrarBatimento(long outboxPendentes, double outboxIdadeSegundos)
    {
        Interlocked.Exchange(ref _outboxPendentesNaOrigem, outboxPendentes);
        Interlocked.Exchange(ref _outboxIdadeNaOrigemMilissegundos, (long)(outboxIdadeSegundos * 1000));
        Interlocked.Exchange(ref _ultimoBatimentoTicks, _relogio.GetUtcNow().UtcTicks);
    }

    /// <summary>
    /// RNF-05, docs/contratos/batimento-do-despachante.v1.md: defasado quando o
    /// projetor nao alcanca o broker; quando o batimento atrasou (a publicacao
    /// em Lancamentos parou); quando a outbox na origem tem evento mais velho
    /// que o limite; ou quando ha fila acumulada aqui e o ultimo evento chegou
    /// atrasado.
    /// </summary>
    public bool Defasado(double? atrasoDoUltimoEventoSegundos, TimeSpan limite, TimeSpan limiteDoBatimento) =>
        !Conectado
        || IdadeDoBatimento > limiteDoBatimento
        || OutboxIdadeNaOrigemSegundos > limite.TotalSeconds
        || (Pendentes > 0 && atrasoDoUltimoEventoSegundos is { } atraso && atraso > limite.TotalSeconds);
}
