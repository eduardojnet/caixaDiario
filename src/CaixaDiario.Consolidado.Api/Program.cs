using System.Threading.RateLimiting;
using CaixaDiario.Consolidado.Api;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole(o => o.IncludeScopes = true);

builder.Services.Configure<OpcoesBroker>(builder.Configuration.GetSection(OpcoesBroker.Secao));
builder.Services.Configure<OpcoesConsulta>(builder.Configuration.GetSection(OpcoesConsulta.Secao));

var conexao = builder.Configuration.GetConnectionString("Consolidado")
    ?? throw new InvalidOperationException("ConnectionStrings:Consolidado nao configurada.");
builder.Services.AddSingleton(_ => NpgsqlDataSource.Create(conexao));
builder.Services.AddSingleton<RepositorioConsolidado>();
builder.Services.AddSingleton<EstadoDoProjetor>();
builder.Services.AddHostedService<ProjetorRabbitMq>();
builder.Services.AddMemoryCache();

// Degradacao controlada (Arquitetura Alvo §4.6): acima do limite, o excedente
// recebe 429 na hora, em vez de acumular e derrubar o servico. Cada 429 conta
// no orcamento de 5% (RNF-03).
var opcoesConsulta = builder.Configuration.GetSection(OpcoesConsulta.Secao).Get<OpcoesConsulta>() ?? new OpcoesConsulta();
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.AddPolicy("consulta", _ => RateLimitPartition.GetTokenBucketLimiter("global", _ => new TokenBucketRateLimiterOptions
    {
        TokenLimit = opcoesConsulta.LimitePorSegundo,
        TokensPerPeriod = Math.Max(1, opcoesConsulta.LimitePorSegundo / 10),
        ReplenishmentPeriod = TimeSpan.FromMilliseconds(100),
        QueueLimit = opcoesConsulta.TamanhoDaFila,
        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
        AutoReplenishment = true,
    }));
});

builder.Services.AddHealthChecks()
    .AddCheck<VerificacaoBanco>("banco-consolidado", tags: ["pronto"]);

var app = builder.Build();

app.UseRateLimiter();
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = c => c.Tags.Contains("pronto") });

app.MapGet("/v1/caixas/{caixaId}/consolidado/{data}", async (
        string caixaId,
        DateOnly data,
        RepositorioConsolidado repositorio,
        IMemoryCache cache,
        EstadoDoProjetor projetor,
        IOptions<OpcoesConsulta> opcoes,
        CancellationToken cancellationToken) =>
    {
        // Cache curto por (caixa, data): a consulta e frequente em pico e muda
        // pouco entre lancamentos. O custo e servir um saldo de instantes atras,
        // visivel em atualizadoAte.
        var saldo = await cache.GetOrCreateAsync((caixaId, data), entrada =>
        {
            entrada.AbsoluteExpirationRelativeToNow = opcoes.Value.DuracaoDoCache;
            return repositorio.ConsultarAsync(
                caixaId, data, atraso => projetor.Defasado(atraso, opcoes.Value.LimiteDeDefasagem), cancellationToken);
        }).ConfigureAwait(false);
        return Results.Ok(saldo);
    })
    .RequireRateLimiting("consulta");

await app.RunAsync().ConfigureAwait(false);
