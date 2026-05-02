using Portivio.API.HealthChecks;
using Portivio.Infrastructure.Data;

namespace Portivio.API.Extensions;

public static class HealthCheckExtensions
{
    public static IServiceCollection AddPortivioHealthChecks(this IServiceCollection services)
    {
        services.AddSingleton<HangfireHealthCheck>();
        services.AddHealthChecks()
            .AddDbContextCheck<PortivioDbContext>()
            .AddCheck<HangfireHealthCheck>("hangfire");
        return services;
    }
}
