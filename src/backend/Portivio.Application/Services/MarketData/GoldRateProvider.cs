using Microsoft.Extensions.Options;

namespace Portivio.Application.Services.MarketData
{
    public interface IGoldRateProvider
    {
        Task<decimal?> GetRatePerGramAsync(string purity, CancellationToken ct = default);
    }

    public class GoldRateProvider : IGoldRateProvider
    {
        private readonly IOptionsMonitor<MarketDataOptions> _options;

        public GoldRateProvider(IOptionsMonitor<MarketDataOptions> options)
        {
            _options = options;
        }

        public Task<decimal?> GetRatePerGramAsync(string purity, CancellationToken ct = default)
        {
            var gold = _options.CurrentValue.Gold;
            if (gold.RatePerGram24K <= 0)
                return Task.FromResult<decimal?>(null);

            var normalized = (purity ?? string.Empty).Trim().ToUpperInvariant();
            return normalized switch
            {
                "24K" => Task.FromResult<decimal?>(gold.RatePerGram24K),
                "22K" => Task.FromResult<decimal?>(gold.RatePerGram24K * gold.Purity22KMultiplier),
                _ => Task.FromResult<decimal?>(null)
            };
        }
    }
}
