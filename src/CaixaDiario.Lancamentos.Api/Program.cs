using CaixaDiario.Lancamentos.Api;
using Dapper;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Npgsql;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole(o => o.IncludeScopes = true);

builder.Services.Configure<OpcoesBroker>(builder.Configuration.GetSection(OpcoesBroker.Secao));
builder.Services.Configure<OpcoesDespacho>(builder.Configuration.GetSection(OpcoesDespacho.Secao));
builder.Services.Configure<OpcoesCaixa>(builder.Configuration.GetSection(OpcoesCaixa.Secao));

var conexao = builder.Configuration.GetConnectionString("Lancamentos")
    ?? throw new InvalidOperationException("ConnectionStrings:Lancamentos nao configurada.");
builder.Services.AddSingleton(_ => NpgsqlDataSource.Create(conexao));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<RepositorioLancamentos>();
builder.Services.AddSingleton<PublicadorRabbitMq>();
builder.Services.AddSingleton<DespachanteOutbox>();
builder.Services.AddSingleton<Telemetria>();

// Observabilidade (ADR-0003): traco do POST ate a publicacao, com os comandos
// do PostgreSQL; metricas do servico e do HTTP; logs. Exporta por OTLP so
// quando OTEL_EXPORTER_OTLP_ENDPOINT esta definido. O exportador e assincrono
// e descarta em caso de falha: o painel fora do ar nao afeta o registro.
var telemetria = builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService("lancamentos"))
    .WithTracing(t => t.AddSource(Telemetria.Nome).AddAspNetCoreInstrumentation().AddNpgsql())
    .WithMetrics(m => m.AddMeter(Telemetria.Nome).AddAspNetCoreInstrumentation())
    .WithLogging();
if (!string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]))
{
    telemetria.UseOtlpExporter();
}
builder.Services.AddHostedService(sp => sp.GetRequiredService<DespachanteOutbox>());

// Prontidao verifica SO o proprio banco. Nunca o broker, nunca o Consolidado
// (regra 1, RNF-01): nada fora da zona critica pode tirar este servico do ar.
builder.Services.AddHealthChecks()
    .AddCheck<VerificacaoBanco>("banco-lancamentos", tags: ["pronto"]);

SqlMapper.AddTypeHandler(new ManipuladorDateOnly());

var app = builder.Build();

app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = c => c.Tags.Contains("pronto") });
app.MapEndpointsDeLancamentos();

await app.RunAsync().ConfigureAwait(false);
