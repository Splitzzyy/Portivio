using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Portivio.Application.Services.MarketData
{
    public class AlphaVantageStockProvider : IStockPriceProvider
    {
        public const string HttpClientName = "AlphaVantage";
        private const string SourceTag = "ALPHAVANTAGE";

        private readonly IHttpClientFactory _httpClientFactory;
        private readonly MarketDataOptions _options;
        private readonly ILogger<AlphaVantageStockProvider> _logger;

        public AlphaVantageStockProvider(
            IHttpClientFactory httpClientFactory,
            IOptions<MarketDataOptions> options,
            ILogger<AlphaVantageStockProvider> logger)
        {
            _httpClientFactory = httpClientFactory;
            _options = options.Value;
            _logger = logger;
        }

        public async Task<StockQuote?> GetQuoteAsync(string symbol, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(symbol))
                return null;

            var apiKey = _options.AlphaVantage.ApiKey;
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                _logger.LogWarning("AlphaVantage API key missing; cannot fetch quote for {Symbol}", symbol);
                return null;
            }

            var client = _httpClientFactory.CreateClient(HttpClientName);
            var url = $"/query?function=GLOBAL_QUOTE&symbol={Uri.EscapeDataString(symbol)}&apikey={Uri.EscapeDataString(apiKey)}";

            using var response = await client.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("AlphaVantage returned {Status} for {Symbol}", response.StatusCode, symbol);
                return null;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);

            if (!doc.RootElement.TryGetProperty("Global Quote", out var quote))
            {
                _logger.LogWarning("AlphaVantage response missing Global Quote for {Symbol}", symbol);
                return null;
            }

            var priceStr = quote.TryGetProperty("05. price", out var p) ? p.GetString() : null;
            var dateStr = quote.TryGetProperty("07. latest trading day", out var d) ? d.GetString() : null;

            if (!decimal.TryParse(priceStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var price) || price <= 0)
                return null;

            var asOf = DateTime.UtcNow;
            if (DateTime.TryParseExact(dateStr, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed))
                asOf = parsed;

            return new StockQuote(symbol.Trim().ToUpperInvariant(), price, asOf, SourceTag);
        }
    }
}
