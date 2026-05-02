using Hangfire;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Portivio.API.HealthChecks;

public class HangfireHealthCheck : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var servers = JobStorage.Current.GetMonitoringApi().Servers();
            return Task.FromResult(servers.Count > 0
                ? HealthCheckResult.Healthy($"{servers.Count} Hangfire server(s) running")
                : HealthCheckResult.Degraded("No Hangfire servers registered"));
        }
        catch (Exception ex)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy("Hangfire storage unreachable", ex));
        }
    }
}
