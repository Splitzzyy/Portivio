using Hangfire;
using Hangfire.PostgreSql;
using Portivio.Application.Services;

namespace Portivio.API.Extensions;

public static class HangfireExtensions
{
    public const string DailyHoldingsRefreshJobId = "refresh-holdings-daily";
    public const string DailyHoldingsRefreshCron = "0 6 * * *";          // 06:00 IST (TimeZone applied below)

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

    public static WebApplication RegisterRecurringJobs(this WebApplication app)
    {
        // MisfireHandlingMode.Strict: if the API container is down at the scheduled
        // 06:00 IST and starts back up later, Hangfire enqueues the missed run.
        // Idempotency comes from PriceHistory's per-(InstrumentId, Date) uniqueness.
        var istZone = TryFindIst();
        RecurringJob.AddOrUpdate<IHoldingRecalculationService>(
            recurringJobId: DailyHoldingsRefreshJobId,
            methodCall: svc => svc.RunDailyRefreshAsync(CancellationToken.None),
            cronExpression: DailyHoldingsRefreshCron,
            options: new RecurringJobOptions
            {
                TimeZone = istZone,
                MisfireHandling = MisfireHandlingMode.Strict
            });

        return app;
    }

    private static TimeZoneInfo TryFindIst()
    {
        // "India Standard Time" on Windows; "Asia/Kolkata" on Linux/macOS.
        try { return TimeZoneInfo.FindSystemTimeZoneById("India Standard Time"); }
        catch (TimeZoneNotFoundException) { }
        try { return TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata"); }
        catch (TimeZoneNotFoundException) { }
        return TimeZoneInfo.Utc;
    }
}
