using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Portivio.Application.DTOs.MarketData;
using Portivio.Application.Results;
using Portivio.Application.Services.MarketData;
using System.Text.Json;

namespace Portivio.API.Controllers
{
    [ApiController]
    [Authorize]
    [EnableRateLimiting("per-user")]
    [Route("api/market")]
    public class MarketDataController : ControllerBase
    {
        private readonly IMarketDataService _marketDataService;
        private readonly IStandardRateService _standardRateService;
        private readonly IHttpClientFactory _httpClientFactory;

        public MarketDataController(IMarketDataService marketDataService, IStandardRateService standardRateService, IHttpClientFactory httpClientFactory)
        {
            _marketDataService = marketDataService;
            _standardRateService = standardRateService;
            _httpClientFactory = httpClientFactory;
        }

        [HttpGet("live-price")]
        public async Task<IActionResult> GetLivePrice([FromQuery] string symbol, [FromQuery] string exchange, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(symbol))
                return BadRequest(new { message = "symbol is required" });

            var suffix = string.Equals(exchange, "BSE", StringComparison.OrdinalIgnoreCase) ? "BO" : "NS";
            var client = _httpClientFactory.CreateClient("LivePriceApi");
            try
            {
                var json = await client.GetStringAsync($"/stock?symbol={Uri.EscapeDataString(symbol.ToUpperInvariant())}.{suffix}&res=num", ct);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                if (root.TryGetProperty("status", out var status) && status.GetString() == "success"
                    && root.TryGetProperty("data", out var data)
                    && data.TryGetProperty("last_price", out var priceEl)
                    && priceEl.TryGetDecimal(out var lastPrice))
                {
                    return Ok(new { lastPrice });
                }
                return NotFound(new { message = $"No price returned for {symbol}" });
            }
            catch (HttpRequestException)
            {
                return StatusCode(502, new { message = "Market data provider unavailable" });
            }
        }

        [HttpGet("stocks/{symbol}")]
        public async Task<IActionResult> GetLatestStockPrice(string symbol, CancellationToken ct)
        {
            var result = await _marketDataService.GetLatestStockPriceAsync(symbol, ct);
            return result.Match(
                onSuccess: () => Ok(result.Data),
                onFailure: (e) => StatusCode(e.StatusCode ?? 400, new { success = false, message = e.Message, errors = e.Errors }));
        }

        [HttpPost("stocks/{symbol}/sync")]
        public async Task<IActionResult> SyncStockPrice(string symbol, CancellationToken ct)
        {
            var result = await _marketDataService.SyncStockPriceAsync(symbol, ct);
            return result.Match(
                onSuccess: () => StatusCode(result.StatusCode ?? 200, result.Data),
                onFailure: (e) => StatusCode(e.StatusCode ?? 400, new { success = false, message = e.Message, errors = e.Errors }));
        }

        [HttpGet("mutual-funds/{isin}")]
        public async Task<IActionResult> GetLatestNav(string isin, CancellationToken ct)
        {
            var result = await _marketDataService.GetLatestNavAsync(isin, ct);
            return result.Match(
                onSuccess: () => Ok(result.Data),
                onFailure: (e) => StatusCode(e.StatusCode ?? 400, new { success = false, message = e.Message, errors = e.Errors }));
        }

        [HttpPost("mutual-funds/{isin}/sync")]
        public async Task<IActionResult> SyncNavByIsin(string isin, CancellationToken ct)
        {
            var result = await _marketDataService.SyncNavByIsinAsync(isin, ct);
            return result.Match(
                onSuccess: () => StatusCode(result.StatusCode ?? 200, result.Data),
                onFailure: (e) => StatusCode(e.StatusCode ?? 400, new { success = false, message = e.Message, errors = e.Errors }));
        }

        [HttpPost("mutual-funds/sync-all")]
        public async Task<IActionResult> SyncAllNavs(CancellationToken ct)
        {
            var result = await _marketDataService.SyncAllNavsAsync(ct);
            return result.Match(
                onSuccess: () => Ok(result.Data),
                onFailure: (e) => StatusCode(e.StatusCode ?? 400, new { success = false, message = e.Message, errors = e.Errors }));
        }

        [HttpGet("rates/ppf")]
        public async Task<IActionResult> GetLatestPpfRate(CancellationToken ct)
        {
            var result = await _standardRateService.GetLatestPpfRateAsync(ct);
            return result.Match(
                onSuccess: () => Ok(result.Data),
                onFailure: (e) => StatusCode(e.StatusCode ?? 400, new { success = false, message = e.Message, errors = e.Errors }));
        }

        [HttpPost("rates/ppf/sync")]
        public async Task<IActionResult> SyncPpfRate(CancellationToken ct)
        {
            var result = await _standardRateService.SyncPpfRateAsync(ct);
            return result.Match(
                onSuccess: () => StatusCode(result.StatusCode ?? 200, result.Data),
                onFailure: (e) => StatusCode(e.StatusCode ?? 400, new { success = false, message = e.Message, errors = e.Errors }));
        }

        [HttpGet("rates/fd")]
        public async Task<IActionResult> GetLatestFdRates([FromQuery] string? bank, [FromQuery] int? tenureMonths, CancellationToken ct)
        {
            var result = await _standardRateService.GetLatestFdRatesAsync(bank, tenureMonths, ct);
            return result.Match(
                onSuccess: () => Ok(result.Data),
                onFailure: (e) => StatusCode(e.StatusCode ?? 400, new { success = false, message = e.Message, errors = e.Errors }));
        }

        [HttpPost("rates/fd/sync")]
        public async Task<IActionResult> SyncFdRates(CancellationToken ct)
        {
            var result = await _standardRateService.SyncFdRatesAsync(ct);
            return result.Match(
                onSuccess: () => Ok(result.Data),
                onFailure: (e) => StatusCode(e.StatusCode ?? 400, new { success = false, message = e.Message, errors = e.Errors }));
        }

        [HttpPost("rates/fd")]
        public async Task<IActionResult> UpsertFdRate([FromBody] UpsertFdRateRequest request, CancellationToken ct)
        {
            var result = await _standardRateService.UpsertFdRateAsync(request, ct);
            return result.Match(
                onSuccess: () => StatusCode(result.StatusCode ?? 200, result.Data),
                onFailure: (e) => StatusCode(e.StatusCode ?? 400, new { success = false, message = e.Message, errors = e.Errors }));
        }
    }
}
