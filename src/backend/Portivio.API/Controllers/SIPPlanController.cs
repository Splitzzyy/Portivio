using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Portivio.Application.DTOs.SIPPlan;
using Portivio.Application.Results;
using Portivio.Application.Services;

namespace Portivio.API.Controllers
{
    [ApiController]
    [Authorize]
    [EnableRateLimiting("per-user")]
    [Route("api/profiles/{profileId:guid}/sip-plans")]
    public class SIPPlanController : PortivioControllerBase
    {
        private readonly ISIPPlanService _sipPlanService;

        public SIPPlanController(ISIPPlanService sipPlanService)
        {
            _sipPlanService = sipPlanService;
        }

        [HttpGet]
        public async Task<IActionResult> GetSIPPlans(Guid profileId, [FromQuery] bool? activeOnly = null)
        {
            if (!TryGetCurrentUserId(out var userId)) return UserNotAuthenticated();
            var result = await _sipPlanService.GetSIPPlansAsync(userId, profileId, activeOnly);
            return result.Match(
                onSuccess: () => Ok(result.Data),
                onFailure: (error) => StatusCode(error.StatusCode ?? 400, new { success = false, message = error.Message, errors = error.Errors })
            );
        }

        [HttpPost]
        public async Task<IActionResult> CreateSIPPlan(Guid profileId, [FromBody] CreateSIPPlanRequest request)
        {
            if (!TryGetCurrentUserId(out var userId)) return UserNotAuthenticated();
            var result = await _sipPlanService.CreateSIPPlanAsync(userId, profileId, request);
            return result.Match(
                onSuccess: () => StatusCode(201, result.Data),
                onFailure: (error) => StatusCode(error.StatusCode ?? 400, new { success = false, message = error.Message, errors = error.Errors })
            );
        }

        [HttpPut("{sipId:guid}")]
        public async Task<IActionResult> UpdateSIPPlan(Guid profileId, Guid sipId, [FromBody] UpdateSIPPlanRequest request)
        {
            if (!TryGetCurrentUserId(out var userId)) return UserNotAuthenticated();
            var result = await _sipPlanService.UpdateSIPPlanAsync(userId, profileId, sipId, request);
            return result.Match(
                onSuccess: () => Ok(result.Data),
                onFailure: (error) => StatusCode(error.StatusCode ?? 400, new { success = false, message = error.Message, errors = error.Errors })
            );
        }

        [HttpPost("{sipId:guid}/activate")]
        public async Task<IActionResult> ActivateSIPPlan(Guid profileId, Guid sipId)
        {
            if (!TryGetCurrentUserId(out var userId)) return UserNotAuthenticated();
            var result = await _sipPlanService.ActivateSIPPlanAsync(userId, profileId, sipId);
            return result.Match(
                onSuccess: () => Ok(result.Data),
                onFailure: (error) => StatusCode(error.StatusCode ?? 400, new { success = false, message = error.Message, errors = error.Errors })
            );
        }

        [HttpPost("{sipId:guid}/deactivate")]
        public async Task<IActionResult> DeactivateSIPPlan(Guid profileId, Guid sipId)
        {
            if (!TryGetCurrentUserId(out var userId)) return UserNotAuthenticated();
            var result = await _sipPlanService.DeactivateSIPPlanAsync(userId, profileId, sipId);
            return result.Match(
                onSuccess: () => Ok(result.Data),
                onFailure: (error) => StatusCode(error.StatusCode ?? 400, new { success = false, message = error.Message, errors = error.Errors })
            );
        }

        [HttpDelete("{sipId:guid}")]
        public async Task<IActionResult> DeleteSIPPlan(Guid profileId, Guid sipId)
        {
            if (!TryGetCurrentUserId(out var userId)) return UserNotAuthenticated();
            var result = await _sipPlanService.DeleteSIPPlanAsync(userId, profileId, sipId);
            return result.Match<IActionResult>(
                onSuccess: () => NoContent(),
                onFailure: (error) => StatusCode(error.StatusCode ?? 400, new { success = false, message = error.Message, errors = error.Errors })
            );
        }
    }
}
