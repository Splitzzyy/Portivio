using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
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
        private readonly IEmailService _emailService;
        private readonly EmailOptions _emailOptions;
        private readonly IWebHostEnvironment _env;

        public EmailTestController(
            IEmailJobService emailJobService,
            IEmailService emailService,
            IOptions<EmailOptions> emailOptions,
            IWebHostEnvironment env)
        {
            _emailJobService = emailJobService;
            _emailService = emailService;
            _emailOptions = emailOptions.Value;
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

        /// <summary>Sends a sample investment summary email (Development only).</summary>
        [HttpPost("investment-summary")]
        public async Task<IActionResult> SendInvestmentSummary([FromQuery] string email, [FromQuery] string name = "Test User")
        {
            if (!_env.IsDevelopment()) return NotFound();
            if (string.IsNullOrWhiteSpace(email)) return BadRequest("email required");

            var frontend = string.IsNullOrWhiteSpace(_emailOptions.FrontendBaseUrl)
                ? "https://app.portivio.app"
                : _emailOptions.FrontendBaseUrl.TrimEnd('/');

            var model = new InvestmentSummaryEmailModel
            {
                UserName = name,
                RegisteredEmail = email,
                GeneratedAtUtc = DateTime.UtcNow,
                ProfileCount = 1,
                HoldingCount = 2,
                TransactionCount = 5,
                ActiveSipCount = 1,
                TotalInvestment = 100000m,
                MarketValue = 112345m,
                UnrealizedPnL = 12345m,
                ReturnPercentage = 12.35m,
                DashboardLink = $"{frontend}/home",
                ManagePreferencesLink = $"{frontend}/home/my-profile",
                IsEmptyAccount = false
            };

            await _emailService.SendInvestmentSummaryAsync(model);
            return Ok(new { sent = true, type = "investment-summary", to = email });
        }
    }
}
