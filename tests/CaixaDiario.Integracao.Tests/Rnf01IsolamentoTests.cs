using System.Diagnostics.Metrics;
using System.Net;
using CaixaDiario.Integracao.Tests.Infra;
using Dapper;
using Npgsql;
using RabbitMQ.Client;
using Testcontainers.RabbitMq;

namespace CaixaDiario.Integracao.Tests;

/// <summary>
/// RNF-01, o teste mais importante da entrega: Lancamentos com banco real,
/// SEM Consolidado e SEM broker, aceita lancamentos; a outbox acumula; com o
/// broker de volta e a fila do consumidor declarada, a outbox esvazia sem
/// intervencao e os eventos chegam a fila.
/// </summary>
public sealed class Rnf01IsolamentoTests(BancoLancamentosIsoladoFixture banco) : IClassFixture<BancoLancamentosIsoladoFixture>
{
    [Fact]
    public async Task Lancamentos_aceita_escrita_sem_Consolidado_e_sem_broker_e_despacha_quando_o_broker_volta()
    {
        // Porta reservada sem nenhum broker escutando. Nenhum Consolidado existe
        // neste teste: nem processo, nem banco.
        var porta = Utilidades.PortaLivre();
        // Batimento curto: garante que ele chegue a fila durante o teste, e que a
        // contagem abaixo separe batimento de evento em toda execucao.
        await using var api = new FabricaLancamentos(banco.ConexaoAplicacao, Broker.Ausente(porta),
            new Dictionary<string, string> { ["Despacho:IntervaloDoBatimento"] = "00:00:00.300" });
        using var cliente = api.CreateClient();
        var caixa = Utilidades.NovoCaixa();

        for (var i = 0; i < 5; i++)
        {
            using var resposta = await cliente.LancarAsync(caixa, $"k-{i}", "CREDITO", 10m + i, Utilidades.Ontem);
            Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);
        }

        using (var pronto = await cliente.GetAsync(new Uri("/health/ready", UriKind.Relative), TestContext.Current.CancellationToken))
        {
            Assert.Equal(HttpStatusCode.OK, pronto.StatusCode);
        }

        Assert.Equal(5, await PendentesAsync(caixa));

        // A metrica da outbox (ADR-0003) mostra o acumulo enquanto o broker esta fora.
        long pendentesMedidos = 0;
        using (var ouvinte = new MeterListener())
        {
            ouvinte.InstrumentPublished = (instrumento, l) =>
            {
                if (instrumento.Name == "caixadiario.outbox.pendentes")
                {
                    l.EnableMeasurementEvents(instrumento);
                }
            };
            ouvinte.SetMeasurementEventCallback<long>((_, valor, _, _) => Interlocked.Exchange(ref pendentesMedidos, valor));
            ouvinte.Start();
            await Utilidades.EsperarAsync(() =>
            {
                ouvinte.RecordObservableInstruments();
                return Task.FromResult(Interlocked.Read(ref pendentesMedidos) >= 5);
            }, TimeSpan.FromSeconds(15), "metrica caixadiario.outbox.pendentes refletir o acumulo");
        }

        // O broker volta, na porta que o despachante ja esta tentando.
        await using var broker = new RabbitMqBuilder("rabbitmq:4.1.8-management-alpine")
            .WithUsername("guest")
            .WithPassword("guest")
            .WithPortBinding(porta, 5672)
            .Build();
        await broker.StartAsync(TestContext.Current.CancellationToken);

        // Sem fila ligada, o publicador nao descarta: o evento continua na outbox.
        await Task.Delay(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);
        Assert.Equal(5, await PendentesAsync(caixa));

        // A topologia que o Consolidado declara ao subir.
        var fabrica = new ConnectionFactory { HostName = "127.0.0.1", Port = porta, UserName = "guest", Password = "guest" };
        await using var conexao = await fabrica.CreateConnectionAsync(TestContext.Current.CancellationToken);
        await using var canal = await conexao.CreateChannelAsync(cancellationToken: TestContext.Current.CancellationToken);
        await canal.ExchangeDeclareAsync("caixadiario.lancamentos", ExchangeType.Fanout, durable: true,
            cancellationToken: TestContext.Current.CancellationToken);
        await canal.QueueDeclareAsync("fila-de-teste", durable: true, exclusive: false, autoDelete: false,
            cancellationToken: TestContext.Current.CancellationToken);
        await canal.QueueBindAsync("fila-de-teste", "caixadiario.lancamentos", string.Empty,
            cancellationToken: TestContext.Current.CancellationToken);

        await Utilidades.EsperarAsync(
            async () => await PendentesAsync(caixa) == 0, TimeSpan.FromSeconds(60), "outbox esvaziar com o broker de volta");
        // Conta so os eventos de lancamento: o batimento do despachante (cartao
        // 43) tambem chega a esta fila, em momento que depende do relogio.
        var eventos = 0;
        while (await canal.BasicGetAsync("fila-de-teste", autoAck: true, TestContext.Current.CancellationToken) is { } mensagem)
        {
            if (mensagem.BasicProperties.Type == "LancamentoRegistrado.v1")
            {
                eventos++;
            }
        }

        Assert.Equal(5, eventos);
    }

    private async Task<long> PendentesAsync(string caixa)
    {
        await using var conexao = new NpgsqlConnection(banco.ConexaoAdmin);
        return await conexao.ExecuteScalarAsync<long>(
            "SELECT count(*) FROM outbox WHERE publicado_em IS NULL AND payload->>'caixaId' = @caixa", new { caixa });
    }
}
