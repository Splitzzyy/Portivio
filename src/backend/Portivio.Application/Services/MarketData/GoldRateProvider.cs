using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Portivio.Application.Services.MarketData
{
    public interface IGoldRateProvider
    {
        Task<decimal?> GetRatePerGramAsync(string purity, CancellationToken ct = default);
    }

    public class GoldRateProvider : IGoldRateProvider
    {
        public const string HttpClientName = "GoldApi";
        public const string SourceTag = "GOLD-API";

        private readonly IOptionsMonitor<MarketDataOptions> _options;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<GoldRateProvider> _logger;

        public GoldRateProvider(
            IOptionsMonitor<MarketDataOptions> options,
            IHttpClientFactory httpClientFactory,
            ILogger<GoldRateProvider> logger)
        {
            _options = options;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        public async Task<decimal?> GetRatePerGramAsync(string purity, CancellationToken ct = default)
        {
            var gold = _options.CurrentValue.Gold;
            if (gold.TroyOunceGrams <= 0)
                return null;

            var normalized = (purity ?? string.Empty).Trim().ToUpperInvariant();
            if (normalized is not ("24K" or "22K"))
                return null;

            try
            {
                var client = _httpClientFactory.CreateClient(HttpClientName);
                var response = await client.GetFromJsonAsync<GoldApiResponse>(gold.PriceUrl, ct);
                if (response is null || response.Price <= 0)
                {
                    _logger.LogWarning("Gold API returned no usable price");
                    return null;
                }

                var rate24K = response.Price / gold.TroyOunceGrams;
                return normalized == "24K"
                    ? rate24K
                    : rate24K * gold.Purity22KMultiplier;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Gold API request failed");
                return null;
            }
        }

        private sealed class GoldApiResponse
        {
            [JsonPropertyName("price")]
            public decimal Price { get; set; }
        }
    }
}
