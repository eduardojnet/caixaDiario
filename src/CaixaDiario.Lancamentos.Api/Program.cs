using CaixaDiario.Lancamentos.Api;
using Dapper;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Npgsql;

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
