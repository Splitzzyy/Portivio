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
        private readonly ILogger<MarketDataController> _logger;

        public MarketDataController(
            IMarketDataService marketDataService,
            IStandardRateService standardRateService,
            ILogger<MarketDataController> logger)
        {
            _marketDataService = marketDataService;
            _standardRateService = standardRateService;
            _logger = logger;
        }

        [HttpGet("stocks/{symbol}")]
        public async Task<IActionResult> GetLatestStockPrice(string symbol, CancellationToken ct)
        {
            try
            {
                var result = await _marketDataService.GetLatestStockPriceAsync(symbol, ct);
                return result.Match(
                    onSuccess: () => Ok(result.Data),
                    onFailure: (e) => StatusCode(e.StatusCode ?? 400, new { success = false, message = e.Message, errors = e.Errors }));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetLatestStockPrice failed for {Symbol}", symbol);
                return StatusCode(500, new { success = false, message = "Error fetching stock price" });
            }
        }

        [HttpPost("stocks/{symbol}/sync")]
        public async Task<IActionResult> SyncStockPrice(string symbol, CancellationToken ct)
        {
            try
            {
                var result = await _marketDataService.SyncStockPriceAsync(symbol, ct);
                return result.Match(
                    onSuccess: () => StatusCode(result.StatusCode ?? 200, result.Data),
                    onFailure: (e) => StatusCode(e.StatusCode ?? 400, new { success = false, message = e.Message, errors = e.Errors }));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SyncStockPrice failed for {Symbol}", symbol);
                return StatusCode(500, new { success = false, message = "Error syncing stock price" });
            }
        }

        [HttpGet("mutual-funds/{isin}")]
        public async Task<IActionResult> GetLatestNav(string isin, CancellationToken ct)
        {
            try
            {
                var result = await _marketDataService.GetLatestNavAsync(isin, ct);
                return result.Match(
                    onSuccess: () => Ok(result.Data),
                    onFailure: (e) => StatusCode(e.StatusCode ?? 400, new { success = false, message = e.Message, errors = e.Errors }));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetLatestNav failed for {Isin}", isin);
                return StatusCode(500, new { success = false, message = "Error fetching NAV" });
            }
        }

        [HttpPost("mutual-funds/{isin}/sync")]
        public async Task<IActionResult> SyncNavByIsin(string isin, CancellationToken ct)
        {
            try
            {
                var result = await _marketDataService.SyncNavByIsinAsync(isin, ct);
                return result.Match(
                    onSuccess: () => StatusCode(result.StatusCode ?? 200, result.Data),
                    onFailure: (e) => StatusCode(e.StatusCode ?? 400, new { success = false, message = e.Message, errors = e.Errors }));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SyncNavByIsin failed for {Isin}", isin);
                return StatusCode(500, new { success = false, message = "Error syncing NAV" });
            }
        }

        [HttpPost("mutual-funds/sync-all")]
        public async Task<IActionResult> SyncAllNavs(CancellationToken ct)
        {
            try
            {
                var result = await _marketDataService.SyncAllNavsAsync(ct);
                return result.Match(
                    onSuccess: () => Ok(result.Data),
                    onFailure: (e) => StatusCode(e.StatusCode ?? 400, new { success = false, message = e.Message, errors = e.Errors }));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SyncAllNavs failed");
                return StatusCode(500, new { success = false, message = "Error syncing NAVs" });
            }
        }

        [HttpGet("rates/ppf")]
        public async Task<IActionResult> GetLatestPpfRate(CancellationToken ct)
        {
            try
            {
                var result = await _standardRateService.GetLatestPpfRateAsync(ct);
                return result.Match(
                    onSuccess: () => Ok(result.Data),
                    onFailure: (e) => StatusCode(e.StatusCode ?? 400, new { success = false, message = e.Message, errors = e.Errors }));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetLatestPpfRate failed");
                return StatusCode(500, new { success = false, message = "Error fetching PPF rate" });
            }
        }

        [HttpPost("rates/ppf/sync")]
        public async Task<IActionResult> SyncPpfRate(CancellationToken ct)
        {
            try
            {
                var result = await _standardRateService.SyncPpfRateAsync(ct);
                return result.Match(
                    onSuccess: () => StatusCode(result.StatusCode ?? 200, result.Data),
                    onFailure: (e) => StatusCode(e.StatusCode ?? 400, new { success = false, message = e.Message, errors = e.Errors }));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SyncPpfRate failed");
                return StatusCode(500, new { success = false, message = "Error syncing PPF rate" });
            }
        }

        [HttpGet("rates/fd")]
        public async Task<IActionResult> GetLatestFdRates([FromQuery] string? bank, [FromQuery] int? tenureMonths, CancellationToken ct)
        {
            try
            {
                var result = await _standardRateService.GetLatestFdRatesAsync(bank, tenureMonths, ct);
                return result.Match(
                    onSuccess: () => Ok(result.Data),
                    onFailure: (e) => StatusCode(e.StatusCode ?? 400, new { success = false, message = e.Message, errors = e.Errors }));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetLatestFdRates failed");
                return StatusCode(500, new { success = false, message = "Error fetching FD rates" });
            }
        }

        [HttpPost("rates/fd/sync")]
        public async Task<IActionResult> SyncFdRates(CancellationToken ct)
        {
            try
            {
                var result = await _standardRateService.SyncFdRatesAsync(ct);
                return result.Match(
                    onSuccess: () => Ok(result.Data),
                    onFailure: (e) => StatusCode(e.StatusCode ?? 400, new { success = false, message = e.Message, errors = e.Errors }));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SyncFdRates failed");
                return StatusCode(500, new { success = false, message = "Error syncing FD rates" });
            }
        }

        [HttpPost("rates/fd")]
        public async Task<IActionResult> UpsertFdRate([FromBody] UpsertFdRateRequest request, CancellationToken ct)
        {
            try
            {
                var result = await _standardRateService.UpsertFdRateAsync(request, ct);
                return result.Match(
                    onSuccess: () => StatusCode(result.StatusCode ?? 200, result.Data),
                    onFailure: (e) => StatusCode(e.StatusCode ?? 400, new { success = false, message = e.Message, errors = e.Errors }));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "UpsertFdRate failed");
                return StatusCode(500, new { success = false, message = "Error upserting FD rate" });
            }
        }
    }
}
