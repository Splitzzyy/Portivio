using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Portivio.Application.DTOs.EmailSummary;
using Portivio.Application.Results;
using Portivio.Application.Services;

namespace Portivio.API.Controllers
{
    [ApiController]
    [Authorize]
    [EnableRateLimiting("per-user")]
    [Route("api/email-summary")]
    public class EmailSummaryController : PortivioControllerBase
    {
        private readonly IEmailSummaryService _emailSummaryService;

        public EmailSummaryController(IEmailSummaryService emailSummaryService)
        {
            _emailSummaryService = emailSummaryService;
        }

        [HttpGet("preferences")]
        public async Task<IActionResult> GetPreferences()
        {
            if (!TryGetCurrentUserId(out var userId)) return UserNotAuthenticated();
            var result = await _emailSummaryService.GetPreferenceAsync(userId);
            return result.Match(
                onSuccess: () => Ok(result.Data),
                onFailure: (error) => StatusCode(error.StatusCode ?? 400, new { success = false, message = error.Message, errors = error.Errors })
            );
        }

        [HttpPut("preferences")]
        public async Task<IActionResult> UpdatePreferences([FromBody] UpdateEmailSummaryPreferenceRequest request)
        {
            if (!TryGetCurrentUserId(out var userId)) return UserNotAuthenticated();
            var result = await _emailSummaryService.UpdatePreferenceAsync(userId, request);
            return result.Match(
                onSuccess: () => Ok(result.Data),
                onFailure: (error) => StatusCode(error.StatusCode ?? 400, new { success = false, message = error.Message, errors = error.Errors })
            );
        }

        [HttpPost("send-now")]
        public async Task<IActionResult> SendNow(CancellationToken cancellationToken)
        {
            if (!TryGetCurrentUserId(out var userId)) return UserNotAuthenticated();
            var result = await _emailSummaryService.QueueManualSummaryAsync(userId, cancellationToken);
            return result.Match(
                onSuccess: () => Ok(result.Data),
                onFailure: (error) => StatusCode(error.StatusCode ?? 400, new { success = false, message = error.Message, errors = error.Errors })
            );
        }
    }
}
