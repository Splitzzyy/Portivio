using Microsoft.AspNetCore.Mvc;
using Portivio.Infrastructure.Services;

namespace Portivio.API.Controllers
{
    /// <summary>
    /// Dev-only controller to manually trigger email jobs for testing.
    /// Returns 404 in non-Development environments.
    /// </summary>
    [ApiController]
    [Route("api/email-test")]
    [Produces("application/json")]
    public class EmailTestController : ControllerBase
    {
        private readonly IEmailJobService _emailJobService;
        private readonly IWebHostEnvironment _env;

        public EmailTestController(IEmailJobService emailJobService, IWebHostEnvironment env)
        {
            _emailJobService = emailJobService;
            _env = env;
        }

        /// <summary>Enqueue a verification email job.</summary>
        [HttpPost("verification")]
        public IActionResult SendVerification([FromQuery] string email, [FromQuery] string name = "Test User")
        {
            if (!_env.IsDevelopment()) return NotFound();
            if (string.IsNullOrWhiteSpace(email)) return BadRequest("email required");

            var token = $"test-token-{Guid.NewGuid():N}";
            _emailJobService.EnqueueVerificationEmail(email, name, token);
            return Ok(new { queued = true, type = "verification", to = email, token });
        }

        /// <summary>Enqueue a welcome email job.</summary>
        [HttpPost("welcome")]
        public IActionResult SendWelcome([FromQuery] string email, [FromQuery] string name = "Test User")
        {
            if (!_env.IsDevelopment()) return NotFound();
            if (string.IsNullOrWhiteSpace(email)) return BadRequest("email required");

            _emailJobService.EnqueueWelcomeEmail(email, name);
            return Ok(new { queued = true, type = "welcome", to = email });
        }

        /// <summary>Enqueue a password reset email job.</summary>
        [HttpPost("password-reset")]
        public IActionResult SendPasswordReset([FromQuery] string email, [FromQuery] string name = "Test User")
        {
            if (!_env.IsDevelopment()) return NotFound();
            if (string.IsNullOrWhiteSpace(email)) return BadRequest("email required");

            var token = $"test-reset-{Guid.NewGuid():N}";
            _emailJobService.EnqueuePasswordResetEmail(email, name, token);
            return Ok(new { queued = true, type = "password-reset", to = email, token });
        }
    }
}
