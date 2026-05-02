using Portivio.API.Extensions;
using Portivio.API.Filters;
using Portivio.API.Middleware;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, services, cfg) =>
    cfg.ReadFrom.Configuration(ctx.Configuration)
       .ReadFrom.Services(services)
       .Enrich.FromLogContext());

var configuration = builder.Configuration;
var environment = builder.Environment;

builder.Services.AddScoped<TransactionFilter>();
builder.Services.AddControllers(options => options.Filters.AddService<TransactionFilter>());
builder.Services.AddOpenApi();
builder.Services.AddForwardedHeadersConfiguration();
builder.Services.AddCorsPolicy(environment, configuration);
builder.Services.AddDatabase(configuration);
builder.Services.AddApplicationServices(configuration);
builder.Services.AddJwtAuthentication(configuration);
builder.Services.AddSwagger();
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
app.MapHangfireDashboardIfDevelopment(environment);

await app.RunWithMigrationAsync();
