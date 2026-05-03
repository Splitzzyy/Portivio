using Microsoft.Extensions.Options;
using Portivio.API.Services;
using Portivio.Application.Services;
using Portivio.Application.Services.Authorization;
using Portivio.Application.Services.MarketData;
using Portivio.Application.Services.Strategies;
using Portivio.Infrastructure.Services;

namespace Portivio.API.Extensions;

public static class ApplicationServicesExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services, IConfiguration configuration)
    {
        RegisterOptions(services, configuration);
        RegisterAuthServices(services);
        RegisterDomainServices(services);
        RegisterEmailServices(services);
        RegisterMarketDataServices(services, configuration);
        return services;
    }

    private static void RegisterOptions(IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<AppSettingsOptions>(configuration.GetSection(AppSettingsOptions.SectionName));
        services.Configure<PostgresOptions>(configuration.GetSection(PostgresOptions.SectionName));
        services.Configure<GoogleAuthOptions>(configuration.GetSection(GoogleAuthOptions.SectionName));
        services.Configure<MarketDataOptions>(configuration.GetSection(MarketDataOptions.SectionName));
        services.Configure<EmailOptions>(configuration.GetSection(EmailOptions.SectionName));
    }

    private static void RegisterAuthServices(IServiceCollection services)
    {
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IAuthHttpContextService, AuthHttpContextService>();
        services.AddHostedService<TokenCleanupService>();
    }

    private static void RegisterDomainServices(IServiceCollection services)
    {
        services.AddScoped<IProfileAccessGuard, ProfileAccessGuard>();
        services.AddScoped<IHomeService, HomeService>();
        services.AddScoped<IProfileService, ProfileService>();
        services.AddScoped<IInstrumentService, InstrumentService>();
        services.AddScoped<IHoldingService, HoldingService>();
        services.AddScoped<ITransactionService, TransactionService>();
        services.AddScoped<ISIPPlanService, SIPPlanService>();
        services.AddScoped<IPriceHistoryService, PriceHistoryService>();
        services.AddScoped<IPortfolioPerformanceService, PortfolioPerformanceService>();
        services.AddScoped<IAssetStrategy, EquityStrategy>();
        services.AddScoped<IAssetStrategy, MutualFundStrategy>();
        services.AddScoped<IAssetStrategy, FixedDepositStrategy>();
        services.AddScoped<IAssetStrategy, RecurringDepositStrategy>();
        services.AddScoped<IAssetStrategy, PpfStrategy>();
        services.AddScoped<IAssetStrategy, GoldStrategy>();
        services.AddScoped<AssetStrategyResolver>();
        services.AddScoped<ITransactionIngestService, TransactionIngestService>();
        services.AddScoped<IAssetInstrumentService, AssetInstrumentService>();
    }

    private static void RegisterEmailServices(IServiceCollection services)
    {
        services.AddScoped<IEmailService, SmtpEmailService>();
        services.AddScoped<IEmailJobService, HangfireEmailJobService>();
    }

    private static void RegisterMarketDataServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddHttpClient(AmfiNavProvider.HttpClientName, (sp, client) =>
        {
            var opts = sp.GetRequiredService<IOptions<MarketDataOptions>>().Value;
            client.Timeout = TimeSpan.FromSeconds(Math.Max(5, opts.Amfi.TimeoutSeconds));
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Portivio/1.0");
        });

        services.AddHttpClient(AlphaVantageStockProvider.HttpClientName, (sp, client) =>
        {
            var opts = sp.GetRequiredService<IOptions<MarketDataOptions>>().Value;
            var baseUrl = string.IsNullOrWhiteSpace(opts.AlphaVantage.BaseUrl)
                ? "https://www.alphavantage.co"
                : opts.AlphaVantage.BaseUrl;
            client.BaseAddress = new Uri(baseUrl);
            client.Timeout = TimeSpan.FromSeconds(Math.Max(5, opts.AlphaVantage.TimeoutSeconds));
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Portivio/1.0");
        });

        services.AddScoped<IStockPriceProvider, AlphaVantageStockProvider>();
        services.AddScoped<IMutualFundNavProvider, AmfiNavProvider>();
        services.AddScoped<IStandardRateProvider, ConfigStandardRateProvider>();
        services.AddScoped<IMarketDataService, MarketDataService>();
        services.AddScoped<IStandardRateService, StandardRateService>();
    }
}
