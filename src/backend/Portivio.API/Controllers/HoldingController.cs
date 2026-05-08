using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Portivio.Application.DTOs.Holding;
using Portivio.Application.Results;
using Portivio.Application.Services;

namespace Portivio.API.Controllers
{
    [ApiController]
    [Authorize]
    [EnableRateLimiting("per-user")]
    [Route("api/profiles/{profileId:guid}/holdings")]
    public class HoldingController : PortivioControllerBase
    {
        private readonly IHoldingService _holdingService;
        private readonly IHoldingRecalculationService _recalc;

        public HoldingController(IHoldingService holdingService, IHoldingRecalculationService recalc)
        {
            _holdingService = holdingService;
            _recalc = recalc;
        }

        [HttpGet]
        public async Task<IActionResult> GetHoldings(Guid profileId)
        {
            if (!TryGetCurrentUserId(out var userId)) return UserNotAuthenticated();
            var result = await _holdingService.GetHoldingsAsync(userId, profileId);
            return result.Match(
                onSuccess: () => Ok(result.Data),
                onFailure: (error) => StatusCode(error.StatusCode ?? 400, new { success = false, message = error.Message, errors = error.Errors })
            );
        }

        [HttpPost]
        public async Task<IActionResult> UpsertHolding(Guid profileId, [FromBody] UpsertHoldingRequest request)
        {
            if (!TryGetCurrentUserId(out var userId)) return UserNotAuthenticated();
            var result = await _holdingService.UpsertHoldingAsync(userId, profileId, request);
            return result.Match(
                onSuccess: () => Ok(result.Data),
                onFailure: (error) => StatusCode(error.StatusCode ?? 400, new { success = false, message = error.Message, errors = error.Errors })
            );
        }

        [HttpPost("refresh")]
        [EnableRateLimiting("manual-refresh")]
        public async Task<IActionResult> RefreshHoldings(Guid profileId, CancellationToken ct)
        {
            if (!TryGetCurrentUserId(out var userId)) return UserNotAuthenticated();
            var result = await _recalc.RefreshProfileAsync(userId, profileId, ct);
            return result.Match(
                onSuccess: () => Ok(result.Data),
                onFailure: (error) => StatusCode(error.StatusCode ?? 400, new { success = false, message = error.Message, errors = error.Errors })
            );
        }

        [HttpDelete("{holdingId:guid}")]
        public async Task<IActionResult> DeleteHolding(Guid profileId, Guid holdingId)
        {
            if (!TryGetCurrentUserId(out var userId)) return UserNotAuthenticated();
            var result = await _holdingService.DeleteHoldingAsync(userId, profileId, holdingId);
            return result.Match<IActionResult>(
                onSuccess: () => NoContent(),
                onFailure: (error) => StatusCode(error.StatusCode ?? 400, new { success = false, message = error.Message, errors = error.Errors })
            );
        }
    }
}
