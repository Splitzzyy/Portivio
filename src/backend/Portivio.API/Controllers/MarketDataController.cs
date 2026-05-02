using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Portivio.Application.DTOs.MarketData;
using Portivio.Application.Results;
using Portivio.Application.Services.MarketData;

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

        public MarketDataController(IMarketDataService marketDataService, IStandardRateService standardRateService)
        {
            _marketDataService = marketDataService;
            _standardRateService = standardRateService;
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
