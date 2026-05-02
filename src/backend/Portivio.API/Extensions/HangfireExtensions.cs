using Hangfire;
using Hangfire.PostgreSql;
using Portivio.Application.Services;

namespace Portivio.API.Extensions;

public static class HangfireExtensions
{
    public static IServiceCollection AddHangfireServices(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration
            .GetSection(PostgresOptions.SectionName)
            .Get<PostgresOptions>()?.ConnectionString
            ?? throw new InvalidOperationException("Postgres connection string missing for Hangfire");

        services.AddHangfire(cfg => cfg
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UsePostgreSqlStorage(opt => opt.UseNpgsqlConnection(connectionString)));
        services.AddHangfireServer();

        return services;
    }

    public static WebApplication MapHangfireDashboardIfDevelopment(this WebApplication app, IWebHostEnvironment environment)
    {
        if (environment.IsDevelopment())
            app.MapHangfireDashboard("/hangfire", new DashboardOptions { Authorization = [] });
        return app;
    }
}
