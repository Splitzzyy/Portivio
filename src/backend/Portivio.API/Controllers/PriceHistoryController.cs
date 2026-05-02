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

        public PriceHistoryController(IPriceHistoryService priceHistoryService)
        {
            _priceHistoryService = priceHistoryService;
        }

        [HttpGet]
        public async Task<IActionResult> GetPriceHistory(Guid instrumentId, [FromQuery] DateTime? from = null, [FromQuery] DateTime? to = null)
        {
            var result = await _priceHistoryService.GetPriceHistoryAsync(instrumentId, from, to);
            return result.Match(
                onSuccess: () => Ok(result.Data),
                onFailure: (error) => StatusCode(error.StatusCode ?? 400, new { success = false, message = error.Message, errors = error.Errors })
            );
        }

        [HttpGet("latest")]
        public async Task<IActionResult> GetLatestPrice(Guid instrumentId)
        {
            var result = await _priceHistoryService.GetLatestPriceAsync(instrumentId);
            return result.Match(
                onSuccess: () => Ok(result.Data),
                onFailure: (error) => StatusCode(error.StatusCode ?? 400, new { success = false, message = error.Message, errors = error.Errors })
            );
        }

        [HttpPost]
        public async Task<IActionResult> AddPrice(Guid instrumentId, [FromBody] AddPriceRequest request)
        {
            var result = await _priceHistoryService.AddPriceAsync(instrumentId, request);
            return result.Match(
                onSuccess: () => StatusCode(201, result.Data),
                onFailure: (error) => StatusCode(error.StatusCode ?? 400, new { success = false, message = error.Message, errors = error.Errors })
            );
        }

        [HttpPost("bulk")]
        public async Task<IActionResult> BulkAddPrices(Guid instrumentId, [FromBody] BulkAddPriceRequest request)
        {
            var result = await _priceHistoryService.BulkAddPricesAsync(instrumentId, request);
            return result.Match(
                onSuccess: () => StatusCode(207, result.Data),
                onFailure: (error) => StatusCode(error.StatusCode ?? 400, new { success = false, message = error.Message, errors = error.Errors })
            );
        }

        [HttpDelete("{priceId:guid}")]
        public async Task<IActionResult> DeletePrice(Guid instrumentId, Guid priceId)
        {
            var result = await _priceHistoryService.DeletePriceAsync(instrumentId, priceId);
            return result.Match<IActionResult>(
                onSuccess: () => NoContent(),
                onFailure: (error) => StatusCode(error.StatusCode ?? 400, new { success = false, message = error.Message, errors = error.Errors })
            );
        }
    }
}
