using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Portivio.API.Filters;
using Portivio.API.Services;
using Portivio.Application.Services;
using Portivio.Application.Services.MarketData;
using Portivio.Infrastructure.Data;
using Serilog;
using System.Security.Claims;
using System.Text;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);
const string FrontendCorsPolicy = "FrontendDevelopment";

var configuration = builder.Configuration;
var environment = builder.Environment;

var postgresOptions = configuration.GetSection(PostgresOptions.SectionName).Get<PostgresOptions>() ?? new PostgresOptions();
if (string.IsNullOrWhiteSpace(postgresOptions.ConnectionString))
    throw new InvalidOperationException("Postgres connection string missing");

// Add services to the container.
builder.Services.AddScoped<TransactionFilter>();
builder.Services.AddControllers(options =>
{
    options.Filters.AddService<TransactionFilter>();
});
builder.Services.AddOpenApi();

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    // Clear default networks to trust all proxies, or restrict to specific IPs:
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

// Add Swagger/Swashbuckle
builder.Services.AddSwaggerGen();

builder.Services.AddCors(options =>
{
    options.AddPolicy(FrontendCorsPolicy, policy =>
    {
        var allowedOrigins = environment.IsDevelopment()
            ? ["http://localhost:4200"]
            : configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];

        if (allowedOrigins.Length == 0)
            return;
        policy
            .WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

// Configure PostgreSQL data access.
builder.Services.AddDbContext<PortivioDbContext>(options =>
{
    options.UseNpgsql(postgresOptions.ConnectionString);
});


// Register Auth Service
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IAuthHttpContextService, AuthHttpContextService>();
builder.Services.AddScoped<IHomeService, HomeService>();
builder.Services.AddScoped<IProfileService, ProfileService>();
builder.Services.AddScoped<IInstrumentService, InstrumentService>();
builder.Services.AddScoped<IHoldingService, HoldingService>();
builder.Services.AddScoped<ITransactionService, TransactionService>();
builder.Services.AddScoped<ISIPPlanService, SIPPlanService>();
builder.Services.AddScoped<IPriceHistoryService, PriceHistoryService>();
builder.Services.AddScoped<IPortfolioPerformanceService, PortfolioPerformanceService>();

//Configure options
builder.Services.Configure<AppSettingsOptions>(configuration.GetSection(AppSettingsOptions.SectionName));
builder.Services.Configure<PostgresOptions>(configuration.GetSection(PostgresOptions.SectionName));
builder.Services.Configure<GoogleAuthOptions>(configuration.GetSection(GoogleAuthOptions.SectionName));
builder.Services.Configure<LoggingOptions>(configuration.GetSection(LoggingOptions.SectionName));

// Market Data
builder.Services.Configure<MarketDataOptions>(configuration.GetSection(MarketDataOptions.SectionName));

builder.Services.AddHttpClient(AmfiNavProvider.HttpClientName, (sp, client) =>
{
    var opts = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<MarketDataOptions>>().Value;
    client.Timeout = TimeSpan.FromSeconds(Math.Max(5, opts.Amfi.TimeoutSeconds));
    client.DefaultRequestHeaders.UserAgent.ParseAdd("Portivio/1.0");
});

builder.Services.AddHttpClient(AlphaVantageStockProvider.HttpClientName, (sp, client) =>
{
    var opts = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<MarketDataOptions>>().Value;
    var baseUrl = string.IsNullOrWhiteSpace(opts.AlphaVantage.BaseUrl)
        ? "https://www.alphavantage.co"
        : opts.AlphaVantage.BaseUrl;
    client.BaseAddress = new Uri(baseUrl);
    client.Timeout = TimeSpan.FromSeconds(Math.Max(5, opts.AlphaVantage.TimeoutSeconds));
    client.DefaultRequestHeaders.UserAgent.ParseAdd("Portivio/1.0");
});

builder.Services.AddScoped<IStockPriceProvider, AlphaVantageStockProvider>();
builder.Services.AddScoped<IMutualFundNavProvider, AmfiNavProvider>();
builder.Services.AddScoped<IStandardRateProvider, ConfigStandardRateProvider>();
builder.Services.AddScoped<IMarketDataService, MarketDataService>();
builder.Services.AddScoped<IStandardRateService, StandardRateService>();

// Configure JWT Authentication
var jwtSettings = configuration.GetSection(AppSettingsOptions.SectionName).Get<AppSettingsOptions>() ?? new AppSettingsOptions();
if (string.IsNullOrWhiteSpace(jwtSettings.Key))
    throw new InvalidOperationException("JWT Key is missing in configuration");

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Key)),
        ValidIssuer = jwtSettings.Issuer,
        ValidAudience = jwtSettings.Audience,
        ClockSkew = TimeSpan.Zero
    };
    options.Events = new JwtBearerEvents
    {
        OnAuthenticationFailed = ctx =>
        {
            // Structured log instead of Console.WriteLine
            Log.Warning("JWT authentication failed for {RequestPath}: {ErrorMessage}",
                ctx.HttpContext.Request.Path,
                ctx.Exception.Message);
            return Task.CompletedTask;
        },
        OnTokenValidated = ctx =>
        {
            var userId = ctx.Principal?.FindFirstValue(ClaimTypes.NameIdentifier)
                      ?? ctx.Principal?.FindFirstValue("sub")
                      ?? "unknown";
            Log.Debug("JWT token validated for UserId={UserId} on {RequestPath}",
                userId,
                ctx.HttpContext.Request.Path);
            return Task.CompletedTask;
        }
    };
});

builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Portivio API",
        Version = "v1",
        Description = "Portivio API with JWT Authentication"
    });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Bearer {your JWT token}"
    });

    c.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Bearer", document)] = []
    });
});

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    //Global limiter
    options.AddFixedWindowLimiter("global", opt =>
    {
        opt.PermitLimit = 100; // 100 requests
        opt.Window = TimeSpan.FromMinutes(1); // per 1 minute
        opt.QueueLimit = 0;
    });

    // Login-specific stricter policy
    options.AddFixedWindowLimiter("login", opt =>
    {
        opt.PermitLimit = 5;
        opt.Window = TimeSpan.FromMinutes(1);
    });

    // Per-user JWT-based limiter
    options.AddPolicy("per-user", context =>
    {
        // JWT subject (best)
        var userId =
            context.User.FindFirst("sub")?.Value
            ?? context.User.FindFirst("userId")?.Value
            ?? context.Connection.RemoteIpAddress?.ToString();

        return RateLimitPartition.GetFixedWindowLimiter(
            userId!,
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 60,
                Window = TimeSpan.FromMinutes(1)
            });
    });

    // For testing purposes
    options.AddFixedWindowLimiter("fixed", opt =>
    {
        opt.Window = TimeSpan.FromMinutes(1);
        opt.PermitLimit = 5;
        opt.QueueLimit = 0;
    });
});


var app = builder.Build();


app.UseForwardedHeaders();

// Configure the HTTP request pipeline.
app.UseStatusCodePages();
app.MapOpenApi();
app.UseSwagger();
app.UseSwaggerUI();
app.UseHttpsRedirection();

app.UseCors(FrontendCorsPolicy);

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// DB Migration on startup with error handling
using (var scope = app.Services.CreateScope())
{
    try
    {
        var db = scope.ServiceProvider.GetRequiredService<PortivioDbContext>();
        db.Database.Migrate();
    }
    catch (Exception ex)
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogCritical(ex, "Database migration failed on startup");
        throw;
    }
}

app.Run();
