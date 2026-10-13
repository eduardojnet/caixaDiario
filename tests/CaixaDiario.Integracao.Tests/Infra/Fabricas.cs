using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using CaixaDiario.Consolidado.Api;
using CaixaDiario.Lancamentos.Api;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace CaixaDiario.Integracao.Tests.Infra;

public sealed record Broker(string Host, int Porta, string Usuario, string Senha)
{
    /// <summary>Porta livre sem broker nenhum: o despachante tenta e falha.</summary>
    public static Broker Ausente(int porta) => new("127.0.0.1", porta, "guest", "guest");
}

/// <summary>API Lancamentos real, em processo, contra o banco da fixture.</summary>
public sealed class FabricaLancamentos(string conexao, Broker broker) : WebApplicationFactory<MarcadorLancamentosApi>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:Lancamentos", conexao);
        builder.UseSetting("Logging:LogLevel:Default", "Warning");
        builder.UseSetting("Broker:Host", broker.Host);
        builder.UseSetting("Broker:Porta", broker.Porta.ToString(System.Globalization.CultureInfo.InvariantCulture));
        builder.UseSetting("Broker:Usuario", broker.Usuario);
        builder.UseSetting("Broker:Senha", broker.Senha);
        builder.UseSetting("Despacho:Intervalo", "00:00:00.200");
        builder.UseSetting("Despacho:EsperaMaxima", "00:00:01");
    }
}

/// <summary>API Consolidado real, em processo, com o projetor ligado ao broker.</summary>
public sealed class FabricaConsolidado(string conexao, Broker broker) : WebApplicationFactory<MarcadorConsolidadoApi>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:Consolidado", conexao);
        builder.UseSetting("Logging:LogLevel:Default", "Warning");
        builder.UseSetting("Broker:Host", broker.Host);
        builder.UseSetting("Broker:Porta", broker.Porta.ToString(System.Globalization.CultureInfo.InvariantCulture));
        builder.UseSetting("Broker:Usuario", broker.Usuario);
        builder.UseSetting("Broker:Senha", broker.Senha);
        builder.UseSetting("Consulta:DuracaoDoCache", "00:00:00.050");
    }
}

public static class Utilidades
{
    /// <summary>Ontem em UTC: nunca e futuro no fuso do caixa (UTC-3).</summary>
    public static DateOnly Ontem => DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1));

    public static string NovoCaixa() => $"caixa-{Guid.NewGuid():N}"[..30];

    public static int PortaLivre()
    {
        using var escuta = new TcpListener(IPAddress.Loopback, 0);
        escuta.Start();
        return ((IPEndPoint)escuta.LocalEndpoint).Port;
    }

    public static async Task<HttpResponseMessage> LancarAsync(
        this HttpClient cliente, string caixaId, string chave, string tipo, decimal valor, DateOnly data,
        string? descricao = null, Guid? estornoDe = null)
    {
        using var requisicao = new HttpRequestMessage(HttpMethod.Post, $"/v1/caixas/{caixaId}/lancamentos")
        {
            Content = JsonContent.Create(new { tipo, valor, dataDoFato = data, descricao, estornoDe }),
        };
        requisicao.Headers.Add("Idempotency-Key", chave);
        return await cliente.SendAsync(requisicao).ConfigureAwait(false);
    }

    public static async Task EsperarAsync(Func<Task<bool>> condicao, TimeSpan limite, string oQue)
    {
        var fim = DateTime.UtcNow + limite;
        while (DateTime.UtcNow < fim)
        {
            if (await condicao().ConfigureAwait(false))
            {
                return;
            }

            await Task.Delay(200).ConfigureAwait(false);
        }

        Assert.Fail($"Tempo esgotado esperando: {oQue}.");
    }
}
