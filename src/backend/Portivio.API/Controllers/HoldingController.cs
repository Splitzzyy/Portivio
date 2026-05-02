using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Portivio.Application.DTOs.Holding;
using Portivio.Application.Results;
using Portivio.Application.Services;
using System.Security.Claims;

namespace Portivio.API.Controllers
{
    [ApiController]
    [Authorize]
    [EnableRateLimiting("per-user")]
    [Route("api/profiles/{profileId:guid}/holdings")]
    public class HoldingController : ControllerBase
    {
        private readonly IHoldingService _holdingService;

        public HoldingController(IHoldingService holdingService)
        {
            _holdingService = holdingService;
        }

        [HttpGet]
        public async Task<IActionResult> GetHoldings(Guid profileId)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
                return Unauthorized(new { success = false, message = "User not authenticated" });

            var result = await _holdingService.GetHoldingsAsync(userId, profileId);
            return result.Match(
                onSuccess: () => Ok(result.Data),
                onFailure: (error) => StatusCode(error.StatusCode ?? 400, new { success = false, message = error.Message, errors = error.Errors })
            );
        }

        [HttpPost]
        public async Task<IActionResult> UpsertHolding(Guid profileId, [FromBody] UpsertHoldingRequest request)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
                return Unauthorized(new { success = false, message = "User not authenticated" });

            var result = await _holdingService.UpsertHoldingAsync(userId, profileId, request);
            return result.Match(
                onSuccess: () => Ok(result.Data),
                onFailure: (error) => StatusCode(error.StatusCode ?? 400, new { success = false, message = error.Message, errors = error.Errors })
            );
        }

        [HttpDelete("{holdingId:guid}")]
        public async Task<IActionResult> DeleteHolding(Guid profileId, Guid holdingId)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
                return Unauthorized(new { success = false, message = "User not authenticated" });

            var result = await _holdingService.DeleteHoldingAsync(userId, profileId, holdingId);
            return result.Match<IActionResult>(
                onSuccess: () => NoContent(),
                onFailure: (error) => StatusCode(error.StatusCode ?? 400, new { success = false, message = error.Message, errors = error.Errors })
            );
        }
    }
}
