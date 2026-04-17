using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Portivio.Application.DTOs.PriceHistory;
using Portivio.Application.Results;
using Portivio.Application.Services;

namespace Portivio.API.Controllers
{
    [ApiController]
    [Authorize]
    [EnableRateLimiting("per-user")]
    [Route("api/instruments/{instrumentId:guid}/prices")]
    public class PriceHistoryController : ControllerBase
    {
        private readonly IPriceHistoryService _priceHistoryService;
        private readonly ILogger<PriceHistoryController> _logger;

        public PriceHistoryController(IPriceHistoryService priceHistoryService, ILogger<PriceHistoryController> logger)
        {
            _priceHistoryService = priceHistoryService;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> GetPriceHistory(Guid instrumentId, [FromQuery] DateTime? from = null, [FromQuery] DateTime? to = null)
        {
            try
            {
                var result = await _priceHistoryService.GetPriceHistoryAsync(instrumentId, from, to);
                return result.Match(
                    onSuccess: () => Ok(result.Data),
                    onFailure: (error) => StatusCode(error.StatusCode ?? 400, new { success = false, message = error.Message, errors = error.Errors })
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching price history for instrument {InstrumentId}", instrumentId);
                return StatusCode(500, new { success = false, message = "An error occurred while fetching price history" });
            }
        }

        [HttpGet("latest")]
        public async Task<IActionResult> GetLatestPrice(Guid instrumentId)
        {
            try
            {
                var result = await _priceHistoryService.GetLatestPriceAsync(instrumentId);
                return result.Match(
                    onSuccess: () => Ok(result.Data),
                    onFailure: (error) => StatusCode(error.StatusCode ?? 400, new { success = false, message = error.Message, errors = error.Errors })
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching latest price for instrument {InstrumentId}", instrumentId);
                return StatusCode(500, new { success = false, message = "An error occurred while fetching the latest price" });
            }
        }

        [HttpPost]
        public async Task<IActionResult> AddPrice(Guid instrumentId, [FromBody] AddPriceRequest request)
        {
            try
            {
                var result = await _priceHistoryService.AddPriceAsync(instrumentId, request);
                return result.Match(
                    onSuccess: () => StatusCode(201, result.Data),
                    onFailure: (error) => StatusCode(error.StatusCode ?? 400, new { success = false, message = error.Message, errors = error.Errors })
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding price for instrument {InstrumentId}", instrumentId);
                return StatusCode(500, new { success = false, message = "An error occurred while adding the price" });
            }
        }

        [HttpPost("bulk")]
        public async Task<IActionResult> BulkAddPrices(Guid instrumentId, [FromBody] BulkAddPriceRequest request)
        {
            try
            {
                var result = await _priceHistoryService.BulkAddPricesAsync(instrumentId, request);
                return result.Match(
                    onSuccess: () => StatusCode(207, result.Data),
                    onFailure: (error) => StatusCode(error.StatusCode ?? 400, new { success = false, message = error.Message, errors = error.Errors })
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error bulk adding prices for instrument {InstrumentId}", instrumentId);
                return StatusCode(500, new { success = false, message = "An error occurred during bulk price import" });
            }
        }

        [HttpDelete("{priceId:guid}")]
        public async Task<IActionResult> DeletePrice(Guid instrumentId, Guid priceId)
        {
            try
            {
                var result = await _priceHistoryService.DeletePriceAsync(instrumentId, priceId);
                return result.Match<IActionResult>(
                    onSuccess: () => NoContent(),
                    onFailure: (error) => StatusCode(error.StatusCode ?? 400, new { success = false, message = error.Message, errors = error.Errors })
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting price {PriceId}", priceId);
                return StatusCode(500, new { success = false, message = "An error occurred while deleting the price" });
            }
        }
    }
}
