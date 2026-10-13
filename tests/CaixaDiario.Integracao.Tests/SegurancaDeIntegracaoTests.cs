using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using CaixaDiario.Integracao.Tests.Infra;
using Microsoft.IdentityModel.Tokens;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;
using Testcontainers.RabbitMq;

namespace CaixaDiario.Integracao.Tests;

/// <summary>
/// Criterio do cartao 37 (ADR-0004): a API recusa requisicao sem token valido
/// ou sem o escopo da operacao, e a credencial do Consolidado no broker nao
/// consegue publicar na exchange de Lancamentos.
/// </summary>
public sealed class SegurancaDeIntegracaoTests(BancoLancamentosFixture bancoLancamentos, BancoConsolidadoFixture bancoConsolidado)
    : IAsyncLifetime
{
    private const ushort AcessoRecusado = 403;

    private readonly RabbitMqContainer _broker = BrokerDeTeste.Criar();

    public async ValueTask InitializeAsync()
    {
        await _broker.StartAsync(TestContext.Current.CancellationToken);
        await BrokerDeTeste.AplicarPermissoesAsync(_broker, TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync() => await _broker.DisposeAsync();

    [Fact]
    public async Task Lancamentos_recusa_sem_token_com_token_invalido_e_sem_escopo()
    {
        await using var api = new FabricaLancamentos(bancoLancamentos.ConexaoAplicacao, Broker.Ausente(Utilidades.PortaLivre()));
        using var cliente = api.CreateClient();
        var caixa = Utilidades.NovoCaixa();

        cliente.DefaultRequestHeaders.Authorization = null;
        using (var semToken = await cliente.LancarAsync(caixa, "s1", "CREDITO", 1m, Utilidades.Ontem))
        {
            Assert.Equal(HttpStatusCode.Unauthorized, semToken.StatusCode);
        }

        using var rsa = RSA.Create(2048);
        var outraChave = new RsaSecurityKey(rsa);
        foreach (var invalido in new[]
        {
            TokensDeTeste.Emitir(TokensDeTeste.TodosOsEscopos, chave: outraChave),
            TokensDeTeste.Emitir(TokensDeTeste.TodosOsEscopos, emissor: "http://outro-emissor"),
            TokensDeTeste.Emitir(TokensDeTeste.TodosOsEscopos, audiencia: "outra-api"),
        })
        {
            cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", invalido);
            using var resposta = await cliente.LancarAsync(caixa, "s2", "CREDITO", 1m, Utilidades.Ontem);
            Assert.Equal(HttpStatusCode.Unauthorized, resposta.StatusCode);
        }

        // Token valido, mas so com leitura: nao pode registrar.
        AutenticacaoDeTeste.Autorizar(cliente, "lancamentos.leitura");
        using (var semEscopo = await cliente.LancarAsync(caixa, "s3", "CREDITO", 1m, Utilidades.Ontem))
        {
            Assert.Equal(HttpStatusCode.Forbidden, semEscopo.StatusCode);
        }

        AutenticacaoDeTeste.Autorizar(cliente, "lancamentos.escrita");
        using (var autorizado = await cliente.LancarAsync(caixa, "s4", "CREDITO", 1m, Utilidades.Ontem))
        {
            Assert.Equal(HttpStatusCode.Created, autorizado.StatusCode);
        }

        // Saude continua aberta: o orquestrador consulta sem token.
        cliente.DefaultRequestHeaders.Authorization = null;
        using var saude = await cliente.GetAsync(new Uri("/health/ready", UriKind.Relative), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, saude.StatusCode);
    }

    [Fact]
    public async Task Consolidado_recusa_consulta_sem_token_e_sem_escopo()
    {
        await using var api = new FabricaConsolidado(bancoConsolidado.ConexaoAplicacao, BrokerDeTeste.Consolidado(_broker));
        using var cliente = api.CreateClient();
        var rota = new Uri($"/v1/caixas/{Utilidades.NovoCaixa()}/consolidado/{Utilidades.Ontem:yyyy-MM-dd}", UriKind.Relative);

        cliente.DefaultRequestHeaders.Authorization = null;
        using (var semToken = await cliente.GetAsync(rota, TestContext.Current.CancellationToken))
        {
            Assert.Equal(HttpStatusCode.Unauthorized, semToken.StatusCode);
        }

        AutenticacaoDeTeste.Autorizar(cliente, "lancamentos.escrita lancamentos.leitura");
        using (var semEscopo = await cliente.GetAsync(rota, TestContext.Current.CancellationToken))
        {
            Assert.Equal(HttpStatusCode.Forbidden, semEscopo.StatusCode);
        }

        AutenticacaoDeTeste.Autorizar(cliente, "consolidado.leitura");
        using var autorizado = await cliente.GetAsync(rota, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, autorizado.StatusCode);
    }

    [Fact]
    public async Task Credencial_do_Consolidado_nao_publica_na_exchange_de_Lancamentos()
    {
        var consolidado = BrokerDeTeste.Consolidado(_broker);
        await using var conexao = await Conectar(consolidado);
        await using var canal = await conexao.CreateChannelAsync(
            new CreateChannelOptions(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true),
            TestContext.Current.CancellationToken);

        // Pode declarar a exchange (para ligar a propria fila), mas nao publicar nela.
        await canal.ExchangeDeclareAsync("caixadiario.lancamentos", ExchangeType.Fanout, durable: true,
            cancellationToken: TestContext.Current.CancellationToken);

        await Assert.ThrowsAnyAsync<Exception>(async () =>
            await canal.BasicPublishAsync("caixadiario.lancamentos", string.Empty, "{\"falso\":true}"u8.ToArray(),
                TestContext.Current.CancellationToken));

        Assert.Equal(AcessoRecusado, canal.CloseReason?.ReplyCode ?? 0);
    }

    [Fact]
    public async Task Credencial_de_Lancamentos_nao_declara_nem_consome_fila()
    {
        var lancamentos = BrokerDeTeste.Lancamentos(_broker);
        await using var conexao = await Conectar(lancamentos);
        await using var canal = await conexao.CreateChannelAsync(cancellationToken: TestContext.Current.CancellationToken);

        var recusa = await Assert.ThrowsAsync<OperationInterruptedException>(async () =>
            await canal.QueueDeclareAsync("consolidado.lancamento-registrado", durable: true, exclusive: false,
                autoDelete: false, cancellationToken: TestContext.Current.CancellationToken));

        Assert.Equal(AcessoRecusado, recusa.ShutdownReason?.ReplyCode ?? 0);
    }

    private static async Task<IConnection> Conectar(Broker broker) =>
        await new ConnectionFactory
        {
            HostName = broker.Host,
            Port = broker.Porta,
            UserName = broker.Usuario,
            Password = broker.Senha,
        }.CreateConnectionAsync(TestContext.Current.CancellationToken);
}
