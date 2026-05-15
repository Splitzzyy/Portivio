using Hangfire;
using Hangfire.PostgreSql;
using Portivio.Application.Services;

namespace Portivio.API.Extensions;

public static class HangfireExtensions
{
    public const string DailyHoldingsRefreshJobId = "refresh-holdings-daily";
    public const string DailyHoldingsRefreshCron = "0 6 * * *";          // 06:00 IST (TimeZone applied below)
    public const string EmailSummaryDispatcherJobId = "email-summary-dispatcher";
    public const string DefaultEmailSummaryDispatcherCron = "*/5 * * * *";

    // Every 15 min during NSE market hours (Mon–Fri 9:00–15:59 IST).
    // Starts slightly before open (9:00 vs 9:15) so first tick is close to open.
    // UpsertLivePriceAsync is idempotent — safe to run multiple times per day.
    public const string MarketHoursRefreshJobId = "refresh-holdings-market-hours";
    public const string MarketHoursRefreshCron = "*/15 9-15 * * 1-5";

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

        // Every 15 min during NSE market hours so users see near-live prices without manual refresh.
        RecurringJob.AddOrUpdate<IHoldingRecalculationService>(
            recurringJobId: MarketHoursRefreshJobId,
            methodCall: svc => svc.RunDailyRefreshAsync(CancellationToken.None),
            cronExpression: MarketHoursRefreshCron,
            options: new RecurringJobOptions
            {
                TimeZone = istZone,
                MisfireHandling = MisfireHandlingMode.Relaxed
            });

        // Enqueue one immediate run on every startup so prices are fresh
        // regardless of when the server last ran.
        BackgroundJob.Enqueue<IHoldingRecalculationService>(
            svc => svc.RunDailyRefreshAsync(CancellationToken.None));

        RegisterEmailSummaryDispatcher(app);

        return app;
    }

    private static void RegisterEmailSummaryDispatcher(WebApplication app)
    {
        if (app.Environment.IsEnvironment("Testing"))
            return;

        var options = app.Configuration
            .GetSection(EmailSummaryOptions.SectionName)
            .Get<EmailSummaryOptions>() ?? new EmailSummaryOptions();

        if (!options.DispatcherEnabled)
            return;

        var cron = string.IsNullOrWhiteSpace(options.DispatcherCron)
            ? DefaultEmailSummaryDispatcherCron
            : options.DispatcherCron;

        RecurringJob.AddOrUpdate<IEmailSummaryService>(
            recurringJobId: EmailSummaryDispatcherJobId,
            methodCall: svc => svc.DispatchDueSchedulesAsync(CancellationToken.None),
            cronExpression: cron);
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
