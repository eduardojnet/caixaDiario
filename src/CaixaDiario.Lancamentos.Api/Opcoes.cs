namespace CaixaDiario.Lancamentos.Api;

public sealed class OpcoesBroker
{
    public const string Secao = "Broker";

    public string Host { get; set; } = "localhost";

    public int Porta { get; set; } = 5672;

    public string Usuario { get; set; } = "guest";

    public string Senha { get; set; } = "guest";

    /// <summary>Exchange fanout do contrato LancamentoRegistrado v1.</summary>
    public string Exchange { get; set; } = "caixadiario.lancamentos";
}

public sealed class OpcoesDespacho
{
    public const string Secao = "Despacho";

    /// <summary>Espera entre varreduras quando a outbox esta vazia.</summary>
    public TimeSpan Intervalo { get; set; } = TimeSpan.FromMilliseconds(500);

    /// <summary>Teto da espera crescente quando o broker esta fora.</summary>
    public TimeSpan EsperaMaxima { get; set; } = TimeSpan.FromSeconds(10);

    public int TamanhoDoLote { get; set; } = 100;
}

public sealed class OpcoesCaixa
{
    public const string Secao = "Caixa";

    /// <summary>Fuso usado para decidir o que e "hoje" na validacao de data futura.</summary>
    public string FusoHorario { get; set; } = "America/Sao_Paulo";
}
