namespace CaixaDiario.Consolidado.Api;

public sealed class OpcoesBroker
{
    public const string Secao = "Broker";

    public string Host { get; set; } = "localhost";

    public int Porta { get; set; } = 5672;

    public string Usuario { get; set; } = "guest";

    public string Senha { get; set; } = "guest";

    public string Exchange { get; set; } = "caixadiario.lancamentos";

    public string Fila { get; set; } = "consolidado.lancamento-registrado";

    /// <summary>Mensagens entregues sem ack ao mesmo tempo.</summary>
    public ushort Prefetch { get; set; } = 50;
}

public sealed class OpcoesConsulta
{
    public const string Secao = "Consulta";

    /// <summary>Validade do cache em memoria por (caixa, data).</summary>
    public TimeSpan DuracaoDoCache { get; set; } = TimeSpan.FromSeconds(2);

    /// <summary>
    /// RNF-05: atraso acima deste limite marca a resposta como defasada.
    /// Proposta de 5 s [NVI] ate medir.
    /// </summary>
    public TimeSpan LimiteDeDefasagem { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Requisicoes aceitas por segundo antes de responder 429. Acima dos 50
    /// req/s do enunciado, para o orcamento de 5% nao ser gasto em operacao normal.
    /// </summary>
    public int LimitePorSegundo { get; set; } = 200;

    /// <summary>Fila curta: espera breve em rajada, recusa rapida acima disso.</summary>
    public int TamanhoDaFila { get; set; } = 50;
}
