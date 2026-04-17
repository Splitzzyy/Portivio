using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Portivio.Application.DTOs.PortfolioPerformance;
using Portivio.Application.Results;
using Portivio.Application.Services;
using System.Security.Claims;

namespace Portivio.API.Controllers
{
    [ApiController]
    [Authorize]
    [EnableRateLimiting("per-user")]
    [Route("api/profiles/{profileId:guid}/performance")]
    public class PortfolioPerformanceController : ControllerBase
    {
        private readonly IPortfolioPerformanceService _performanceService;
        private readonly ILogger<PortfolioPerformanceController> _logger;

        public PortfolioPerformanceController(IPortfolioPerformanceService performanceService, ILogger<PortfolioPerformanceController> logger)
        {
            _performanceService = performanceService;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> GetPerformanceHistory(Guid profileId, [FromQuery] int days = 90)
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
                if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
                    return Unauthorized(new { success = false, message = "User not authenticated" });

                var result = await _performanceService.GetPerformanceHistoryAsync(userId, profileId, days);
                return result.Match(
                    onSuccess: () => Ok(result.Data),
                    onFailure: (error) => StatusCode(error.StatusCode ?? 400, new { success = false, message = error.Message, errors = error.Errors })
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching performance history for profile {ProfileId}", profileId);
                return StatusCode(500, new { success = false, message = "An error occurred while fetching performance history" });
            }
        }

        [HttpGet("latest")]
        public async Task<IActionResult> GetLatestPerformance(Guid profileId)
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
                if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
                    return Unauthorized(new { success = false, message = "User not authenticated" });

                var result = await _performanceService.GetLatestPerformanceAsync(userId, profileId);
                return result.Match(
                    onSuccess: () => Ok(result.Data),
                    onFailure: (error) => StatusCode(error.StatusCode ?? 400, new { success = false, message = error.Message, errors = error.Errors })
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching latest performance for profile {ProfileId}", profileId);
                return StatusCode(500, new { success = false, message = "An error occurred while fetching latest performance" });
            }
        }

        [HttpPost("snapshot")]
        public async Task<IActionResult> RecordSnapshot(Guid profileId, [FromBody] RecordSnapshotRequest? request = null)
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
                if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
                    return Unauthorized(new { success = false, message = "User not authenticated" });

                var result = await _performanceService.RecordSnapshotAsync(userId, profileId, request);
                return result.Match(
                    onSuccess: () => Ok(result.Data),
                    onFailure: (error) => StatusCode(error.StatusCode ?? 400, new { success = false, message = error.Message, errors = error.Errors })
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error recording performance snapshot for profile {ProfileId}", profileId);
                return StatusCode(500, new { success = false, message = "An error occurred while recording the snapshot" });
            }
        }
    }
}
