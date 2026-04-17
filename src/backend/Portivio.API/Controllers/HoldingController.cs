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
        private readonly ILogger<HoldingController> _logger;

        public HoldingController(IHoldingService holdingService, ILogger<HoldingController> logger)
        {
            _holdingService = holdingService;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> GetHoldings(Guid profileId)
        {
            try
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
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching holdings for profile {ProfileId}", profileId);
                return StatusCode(500, new { success = false, message = "An error occurred while fetching holdings" });
            }
        }

        [HttpPost]
        public async Task<IActionResult> UpsertHolding(Guid profileId, [FromBody] UpsertHoldingRequest request)
        {
            try
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
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error upserting holding for profile {ProfileId}", profileId);
                return StatusCode(500, new { success = false, message = "An error occurred while upserting the holding" });
            }
        }

        [HttpDelete("{holdingId:guid}")]
        public async Task<IActionResult> DeleteHolding(Guid profileId, Guid holdingId)
        {
            try
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
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting holding {HoldingId}", holdingId);
                return StatusCode(500, new { success = false, message = "An error occurred while deleting the holding" });
            }
        }
    }
}
