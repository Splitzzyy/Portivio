using Hangfire;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Portivio.API.Extensions;
using Portivio.API.Filters;
using Portivio.API.Middleware;
using Portivio.API.Testing;
using Portivio.Application.Services;
using Serilog;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, services, cfg) =>
    cfg.ReadFrom.Configuration(ctx.Configuration)
       .ReadFrom.Services(services)
       .Enrich.FromLogContext());

var configuration = builder.Configuration;
var environment = builder.Environment;
var isTesting = environment.IsEnvironment("Testing");

if (!isTesting)
{
    builder.Services.AddScoped<TransactionFilter>();
    builder.Services.AddControllers(options => options.Filters.AddService<TransactionFilter>())
        .AddJsonOptions(options =>
        {
            options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        });
}
else
{
    builder.Services.AddControllers()
        .AddJsonOptions(options =>
        {
            options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        });
}
builder.Services.AddOpenApi();
builder.Services.AddForwardedHeadersConfiguration();
builder.Services.AddCorsPolicy(environment, configuration);
if (!isTesting)
    builder.Services.AddDatabase(configuration);
builder.Services.AddApplicationServices(configuration);
if (isTesting)
{
    builder.Services.RemoveAll<IHostedService>();
}
if (!isTesting)
{
    builder.Services.AddJwtAuthentication(configuration);
}
else
{
    builder.Services.AddAuthentication(TestAuthHandler.SchemeName)
        .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });
    builder.Services.AddAuthorization();
}
builder.Services.AddSwagger();
if (!isTesting)
    builder.Services.AddHangfireServices(configuration);
builder.Services.AddPortivioHealthChecks();
builder.Services.AddPortivioRateLimiting();

var app = builder.Build();

app.UseMiddleware<GlobalExceptionMiddleware>();
app.UseForwardedHeaders();
app.UseStatusCodePages();
app.UsePortivioSwagger();
app.UseHttpsRedirection();
app.UseCors(InfrastructureExtensions.FrontendCorsPolicy);
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.MapControllers();
app.MapHealthChecks("/health").AllowAnonymous();
if (!isTesting)
{
    app.MapHangfireDashboardIfDevelopment(environment);
    app.RegisterRecurringJobs();
}

// Boot-time refresh: cron is daily 06:00 IST, but if the API just started up
// outside that window, enqueue one fresh run so users don't sit on stale prices
// until tomorrow. Idempotent on same-day re-runs (PriceHistory per-day uniqueness).
if (!isTesting)
{
    try
    {
        var jobClient = app.Services.GetRequiredService<IBackgroundJobClient>();
        jobClient.Enqueue<IHoldingRecalculationService>(svc => svc.RunDailyRefreshAsync(CancellationToken.None));
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning(ex, "Startup holdings refresh enqueue failed; daily cron will still run");
    }
}

if (!isTesting)
    await app.RunWithMigrationAsync();
else
    await app.RunAsync();

public partial class Program { }
