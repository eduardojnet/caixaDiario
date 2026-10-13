namespace CaixaDiario.Consolidado.Api;

public sealed class OpcoesBroker
{
    public const string Secao = "Broker";

    public string Host { get; set; } = "localhost";

    public int Porta { get; set; } = 5672;

    public string Usuario { get; set; } = "guest";

    public string Senha { get; set; } = "guest";

    /// <summary>Conexao AMQP com TLS (ADR-0004). Ligado no compose, na porta 5671.</summary>
    public bool Tls { get; set; }

    /// <summary>Certificado da autoridade certificadora local que assina o do broker.</summary>
    public string CaminhoCa { get; set; } = "/certs/ca.pem";

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
    /// Sem batimento do despachante por mais que isto, a resposta sai defasada
    /// (cartao 43). Tres intervalos do batimento de Lancamentos.
    /// </summary>
    public TimeSpan LimiteDoBatimento { get; set; } = TimeSpan.FromSeconds(15);

    /// <summary>
    /// Requisicoes aceitas por segundo antes de responder 429. Acima dos 50
    /// req/s do enunciado, para o orcamento de 5% nao ser gasto em operacao normal.
    /// </summary>
    public int LimitePorSegundo { get; set; } = 200;

    /// <summary>Fila curta: espera breve em rajada, recusa rapida acima disso.</summary>
    public int TamanhoDaFila { get; set; } = 50;
}
