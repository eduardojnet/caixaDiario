using System.Threading.RateLimiting;
using CaixaDiario.Consolidado.Api;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Npgsql;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

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
builder.Services.AddSingleton<Telemetria>();
builder.Services.AddHostedService<ProjetorRabbitMq>();

// Observabilidade (ADR-0003): traco da projecao como continuacao do POST de
// Lancamentos; metricas do projetor, do HTTP (orcamento de 5% por codigo de
// resposta) e do limitador; logs. OTLP so com OTEL_EXPORTER_OTLP_ENDPOINT.
var telemetria = builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService("consolidado"))
    .WithTracing(t => t.AddSource(Telemetria.Nome).AddAspNetCoreInstrumentation().AddNpgsql())
    .WithMetrics(m => m
        .AddMeter(Telemetria.Nome, "Microsoft.AspNetCore.RateLimiting")
        .AddAspNetCoreInstrumentation())
    .WithLogging();
if (!string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]))
{
    telemetria.UseOtlpExporter();
}
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

// OAuth2 client credentials (ADR-0004): consulta exige o escopo consolidado.leitura.
const string EscopoLeitura = "consolidado.leitura";
builder.Services.AdicionarAutenticacao(builder.Configuration, EscopoLeitura);

builder.Services.AddHealthChecks()
    .AddCheck<VerificacaoBanco>("banco-consolidado", tags: ["pronto"]);

var app = builder.Build();

// Limitador antes da autenticacao: rajada sem token tambem e contida.
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
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
                caixaId, data,
                atraso => projetor.Defasado(atraso, opcoes.Value.LimiteDeDefasagem, opcoes.Value.LimiteDoBatimento),
                cancellationToken);
        }).ConfigureAwait(false);
        return Results.Ok(saldo);
    })
    .RequireRateLimiting("consulta")
    .RequireAuthorization(EscopoLeitura);

await app.RunAsync().ConfigureAwait(false);
