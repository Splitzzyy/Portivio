using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Portivio.Application.DTOs.Profile;
using Portivio.Application.Results;
using Portivio.Application.Services;
using System.Security.Claims;

namespace Portivio.API.Controllers
{
    [ApiController]
    [Authorize]
    [EnableRateLimiting("per-user")]
    [Route("api/profiles")]
    public class ProfileController : ControllerBase
    {
        private readonly IProfileService _profileService;
        private readonly ILogger<ProfileController> _logger;

        public ProfileController(IProfileService profileService, ILogger<ProfileController> logger)
        {
            _profileService = profileService;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> GetProfiles()
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
                if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
                    return Unauthorized(new { success = false, message = "User not authenticated" });

                var result = await _profileService.GetProfilesAsync(userId);
                return result.Match(
                    onSuccess: () => Ok(result.Data),
                    onFailure: (error) => StatusCode(error.StatusCode ?? 400, new { success = false, message = error.Message, errors = error.Errors })
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching profiles");
                return StatusCode(500, new { success = false, message = "An error occurred while fetching profiles" });
            }
        }

        [HttpPost]
        public async Task<IActionResult> CreateProfile([FromBody] CreateProfileRequest request)
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
                if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
                    return Unauthorized(new { success = false, message = "User not authenticated" });

                var result = await _profileService.CreateProfileAsync(userId, request);
                return result.Match(
                    onSuccess: () => StatusCode(201, result.Data),
                    onFailure: (error) => StatusCode(error.StatusCode ?? 400, new { success = false, message = error.Message, errors = error.Errors })
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating profile");
                return StatusCode(500, new { success = false, message = "An error occurred while creating the profile" });
            }
        }

        [HttpPut("{profileId:guid}")]
        public async Task<IActionResult> UpdateProfile(Guid profileId, [FromBody] UpdateProfileRequest request)
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
                if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
                    return Unauthorized(new { success = false, message = "User not authenticated" });

                var result = await _profileService.UpdateProfileAsync(userId, profileId, request);
                return result.Match(
                    onSuccess: () => Ok(result.Data),
                    onFailure: (error) => StatusCode(error.StatusCode ?? 400, new { success = false, message = error.Message, errors = error.Errors })
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating profile {ProfileId}", profileId);
                return StatusCode(500, new { success = false, message = "An error occurred while updating the profile" });
            }
        }

        [HttpDelete("{profileId:guid}")]
        public async Task<IActionResult> DeleteProfile(Guid profileId)
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
                if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
                    return Unauthorized(new { success = false, message = "User not authenticated" });

                var result = await _profileService.DeleteProfileAsync(userId, profileId);
                return result.Match<IActionResult>(
                    onSuccess: () => NoContent(),
                    onFailure: (error) => StatusCode(error.StatusCode ?? 400, new { success = false, message = error.Message, errors = error.Errors })
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting profile {ProfileId}", profileId);
                return StatusCode(500, new { success = false, message = "An error occurred while deleting the profile" });
            }
        }
    }
}
