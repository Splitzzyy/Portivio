using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Portivio.Application.Services.MarketData
{
    public interface ILivePriceApiStockProvider
    {
        /// <summary>ticker format: SYMBOL.NS or SYMBOL.BO</summary>
        Task<StockQuote?> GetQuoteAsync(string ticker, CancellationToken ct = default);
    }

    public class LivePriceApiStockProvider : ILivePriceApiStockProvider
    {
        public const string HttpClientName = "LivePriceApi";
        private const string SourceTag = "LIVEPRICEAPI";

        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<LivePriceApiStockProvider> _logger;

        public LivePriceApiStockProvider(IHttpClientFactory httpClientFactory, ILogger<LivePriceApiStockProvider> logger)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        public async Task<StockQuote?> GetQuoteAsync(string ticker, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(ticker))
                return null;

            var client = _httpClientFactory.CreateClient(HttpClientName);
            try
            {
                var json = await client.GetStringAsync(
                    $"/stock?symbol={Uri.EscapeDataString(ticker.ToUpperInvariant())}&res=num", ct);

                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (!root.TryGetProperty("status", out var status) || status.GetString() != "success")
                {
                    _logger.LogWarning("LivePriceApi non-success for {Ticker}", ticker);
                    return null;
                }

                if (!root.TryGetProperty("data", out var data) ||
                    !data.TryGetProperty("last_price", out var priceEl) ||
                    !priceEl.TryGetDecimal(out var price) || price <= 0)
                {
                    _logger.LogWarning("LivePriceApi missing last_price for {Ticker}", ticker);
                    return null;
                }

                var symbol = ticker.Contains('.') ? ticker[..ticker.IndexOf('.')] : ticker;
                return new StockQuote(symbol.ToUpperInvariant(), price, DateTime.UtcNow, SourceTag);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "LivePriceApi request failed for {Ticker}", ticker);
                return null;
            }
        }
    }
}
