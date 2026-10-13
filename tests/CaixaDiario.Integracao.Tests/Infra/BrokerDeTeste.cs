using Testcontainers.RabbitMq;

namespace CaixaDiario.Integracao.Tests.Infra;

/// <summary>
/// RabbitMQ real com as credenciais por servico aplicadas pelo MESMO script do
/// compose (infra/rabbitmq/permissoes.sh), executado dentro do contêiner.
/// </summary>
public static class BrokerDeTeste
{
    public const string SenhaLancamentos = "senha-lancamentos-teste";
    public const string SenhaConsolidado = "senha-consolidado-teste";

    private static readonly byte[] Script = File.ReadAllBytes(
        Path.Combine(AppContext.BaseDirectory, "infra", "permissoes.sh"));

    public static RabbitMqContainer Criar(int? portaFixa = null)
    {
        var construtor = new RabbitMqBuilder("rabbitmq:4.1.8-management-alpine")
            .WithUsername("guest")
            .WithPassword("guest")
            .WithResourceMapping(Script, "/opt/caixadiario/permissoes.sh");
        return (portaFixa is { } porta ? construtor.WithPortBinding(porta, 5672) : construtor).Build();
    }

    public static async Task AplicarPermissoesAsync(RabbitMqContainer broker, CancellationToken cancellationToken)
    {
        var resultado = await broker.ExecAsync(
            ["sh", "-c",
             $"BROKER_LANCAMENTOS_PASSWORD={SenhaLancamentos} BROKER_CONSOLIDADO_PASSWORD={SenhaConsolidado} sh /opt/caixadiario/permissoes.sh"],
            cancellationToken).ConfigureAwait(false);
        Assert.True(resultado.ExitCode == 0, $"permissoes.sh falhou: {resultado.Stdout} {resultado.Stderr}");
    }

    public static Broker Lancamentos(RabbitMqContainer broker) =>
        new(broker.Hostname, broker.GetMappedPublicPort(5672), "lancamentos", SenhaLancamentos);

    public static Broker Consolidado(RabbitMqContainer broker) =>
        new(broker.Hostname, broker.GetMappedPublicPort(5672), "consolidado", SenhaConsolidado);
}
