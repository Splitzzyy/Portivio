using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Portivio.Application.DTOs.PortfolioPerformance;
using Portivio.Application.Results;
using Portivio.Application.Services;

namespace Portivio.API.Controllers
{
    [ApiController]
    [Authorize]
    [EnableRateLimiting("per-user")]
    [Route("api/profiles/{profileId:guid}/performance")]
    public class PortfolioPerformanceController : PortivioControllerBase
    {
        private readonly IPortfolioPerformanceService _performanceService;

        public PortfolioPerformanceController(IPortfolioPerformanceService performanceService)
        {
            _performanceService = performanceService;
        }

        [HttpGet]
        public async Task<IActionResult> GetPerformanceHistory(Guid profileId, [FromQuery] int days = 90)
        {
            if (!TryGetCurrentUserId(out var userId)) return UserNotAuthenticated();
            var result = await _performanceService.GetPerformanceHistoryAsync(userId, profileId, days);
            return result.Match(
                onSuccess: () => Ok(result.Data),
                onFailure: (error) => StatusCode(error.StatusCode ?? 400, new { success = false, message = error.Message, errors = error.Errors })
            );
        }

        [HttpGet("latest")]
        public async Task<IActionResult> GetLatestPerformance(Guid profileId)
        {
            if (!TryGetCurrentUserId(out var userId)) return UserNotAuthenticated();
            var result = await _performanceService.GetLatestPerformanceAsync(userId, profileId);
            return result.Match(
                onSuccess: () => Ok(result.Data),
                onFailure: (error) => StatusCode(error.StatusCode ?? 400, new { success = false, message = error.Message, errors = error.Errors })
            );
        }

        [HttpPost("snapshot")]
        public async Task<IActionResult> RecordSnapshot(Guid profileId, [FromBody] RecordSnapshotRequest? request = null)
        {
            if (!TryGetCurrentUserId(out var userId)) return UserNotAuthenticated();
            var result = await _performanceService.RecordSnapshotAsync(userId, profileId, request);
            return result.Match(
                onSuccess: () => Ok(result.Data),
                onFailure: (error) => StatusCode(error.StatusCode ?? 400, new { success = false, message = error.Message, errors = error.Errors })
            );
        }
    }
}
