using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;

namespace CaixaDiario.Consolidado.Api;

internal sealed class VerificacaoBanco(NpgsqlDataSource dataSource) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var comando = dataSource.CreateCommand("SELECT 1");
            await comando.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            return HealthCheckResult.Healthy();
        }
        catch (NpgsqlException ex)
        {
            return HealthCheckResult.Unhealthy("Banco do consolidado indisponivel.", ex);
        }
    }
}
