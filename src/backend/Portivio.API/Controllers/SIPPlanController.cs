using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Portivio.Application.DTOs.SIPPlan;
using Portivio.Application.Results;
using Portivio.Application.Services;
using System.Security.Claims;

namespace Portivio.API.Controllers
{
    [ApiController]
    [Authorize]
    [EnableRateLimiting("per-user")]
    [Route("api/profiles/{profileId:guid}/sip-plans")]
    public class SIPPlanController : ControllerBase
    {
        private readonly ISIPPlanService _sipPlanService;
        private readonly ILogger<SIPPlanController> _logger;

        public SIPPlanController(ISIPPlanService sipPlanService, ILogger<SIPPlanController> logger)
        {
            _sipPlanService = sipPlanService;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> GetSIPPlans(Guid profileId, [FromQuery] bool? activeOnly = null)
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
                if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
                    return Unauthorized(new { success = false, message = "User not authenticated" });

                var result = await _sipPlanService.GetSIPPlansAsync(userId, profileId, activeOnly);
                return result.Match(
                    onSuccess: () => Ok(result.Data),
                    onFailure: (error) => StatusCode(error.StatusCode ?? 400, new { success = false, message = error.Message, errors = error.Errors })
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching SIP plans for profile {ProfileId}", profileId);
                return StatusCode(500, new { success = false, message = "An error occurred while fetching SIP plans" });
            }
        }

        [HttpPost]
        public async Task<IActionResult> CreateSIPPlan(Guid profileId, [FromBody] CreateSIPPlanRequest request)
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
                if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
                    return Unauthorized(new { success = false, message = "User not authenticated" });

                var result = await _sipPlanService.CreateSIPPlanAsync(userId, profileId, request);
                return result.Match(
                    onSuccess: () => StatusCode(201, result.Data),
                    onFailure: (error) => StatusCode(error.StatusCode ?? 400, new { success = false, message = error.Message, errors = error.Errors })
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating SIP plan for profile {ProfileId}", profileId);
                return StatusCode(500, new { success = false, message = "An error occurred while creating the SIP plan" });
            }
        }

        [HttpPut("{sipId:guid}")]
        public async Task<IActionResult> UpdateSIPPlan(Guid profileId, Guid sipId, [FromBody] UpdateSIPPlanRequest request)
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
                if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
                    return Unauthorized(new { success = false, message = "User not authenticated" });

                var result = await _sipPlanService.UpdateSIPPlanAsync(userId, profileId, sipId, request);
                return result.Match(
                    onSuccess: () => Ok(result.Data),
                    onFailure: (error) => StatusCode(error.StatusCode ?? 400, new { success = false, message = error.Message, errors = error.Errors })
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating SIP plan {SipId}", sipId);
                return StatusCode(500, new { success = false, message = "An error occurred while updating the SIP plan" });
            }
        }

        [HttpPost("{sipId:guid}/activate")]
        public async Task<IActionResult> ActivateSIPPlan(Guid profileId, Guid sipId)
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
                if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
                    return Unauthorized(new { success = false, message = "User not authenticated" });

                var result = await _sipPlanService.ActivateSIPPlanAsync(userId, profileId, sipId);
                return result.Match(
                    onSuccess: () => Ok(result.Data),
                    onFailure: (error) => StatusCode(error.StatusCode ?? 400, new { success = false, message = error.Message, errors = error.Errors })
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error activating SIP plan {SipId}", sipId);
                return StatusCode(500, new { success = false, message = "An error occurred while activating the SIP plan" });
            }
        }

        [HttpPost("{sipId:guid}/deactivate")]
        public async Task<IActionResult> DeactivateSIPPlan(Guid profileId, Guid sipId)
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
                if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
                    return Unauthorized(new { success = false, message = "User not authenticated" });

                var result = await _sipPlanService.DeactivateSIPPlanAsync(userId, profileId, sipId);
                return result.Match(
                    onSuccess: () => Ok(result.Data),
                    onFailure: (error) => StatusCode(error.StatusCode ?? 400, new { success = false, message = error.Message, errors = error.Errors })
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deactivating SIP plan {SipId}", sipId);
                return StatusCode(500, new { success = false, message = "An error occurred while deactivating the SIP plan" });
            }
        }

        [HttpDelete("{sipId:guid}")]
        public async Task<IActionResult> DeleteSIPPlan(Guid profileId, Guid sipId)
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
                if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
                    return Unauthorized(new { success = false, message = "User not authenticated" });

                var result = await _sipPlanService.DeleteSIPPlanAsync(userId, profileId, sipId);
                return result.Match<IActionResult>(
                    onSuccess: () => NoContent(),
                    onFailure: (error) => StatusCode(error.StatusCode ?? 400, new { success = false, message = error.Message, errors = error.Errors })
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting SIP plan {SipId}", sipId);
                return StatusCode(500, new { success = false, message = "An error occurred while deleting the SIP plan" });
            }
        }
    }
}
