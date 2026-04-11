using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Portivio.API.Services;
using Portivio.Application.DTOs.Auth;
using Portivio.Application.Results;
using Portivio.Application.Services;
using System.Security.Claims;

namespace Portivio.API.Controllers
{
    [Authorize]
    [EnableRateLimiting("global")]
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;
        private readonly IAuthHttpContextService _authHttpContextService;
        private readonly ILogger<AuthController> _logger;

        public AuthController(IAuthService authService, IAuthHttpContextService authHttpContextService, ILogger<AuthController> logger)
        {
            _authService = authService;
            _authHttpContextService = authHttpContextService;
            _logger = logger;
        }

        /// <summary>
        /// Login user with email and password.
        /// Refresh tokens are issued for both mobile and browser clients.
        /// Mobile receives the token in the response body, while browsers receive it via HttpOnly cookie.
        /// </summary>
        [HttpPost("login")]
        [AllowAnonymous]
        public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginRequest request)
        {
            try
            {
                var result = await _authService.LoginAsync(_authHttpContextService.CreateLoginRequest(HttpContext, request));

                return result.Match(
                    onSuccess: () =>
                    {
                        _authHttpContextService.ApplyRefreshTokenCookie(Response, result.Data);
                        return Ok(_authHttpContextService.CreateClientAuthResponse(HttpContext, result.Data));
                    },
                    onFailure: (error) => StatusCode(error.StatusCode ?? 400, new { success = false, message = error.Message, errors = error.Errors })
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during login");
                return StatusCode(500, new { success = false, message = "An error occurred during login" });
            }
        }

        /// <summary>
        /// Register a new user
        /// </summary>
        [HttpPost("signup")]
        [AllowAnonymous]
        [EnableRateLimiting("login")]
        public async Task<ActionResult<AuthResponse>> Signup([FromBody] SignupRequest request)
        {
            try
            {
                var result = await _authService.SignupAsync(request);

                return result.Match(
                    onSuccess: () => Created("", result.Data),
                    onFailure: (error) => StatusCode(error.StatusCode ?? 400, new { success = false, message = error.Message, errors = error.Errors })
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during signup");
                return StatusCode(500, new { success = false, message = "An error occurred during signup" });
            }
        }

        /// <summary>
        /// Refresh access token using refresh token
        /// </summary>
        [HttpPost("refresh-token")]
        public async Task<ActionResult<AuthResponse>> RefreshToken([FromBody] RefreshTokenRequest request)
        {
            try
            {
                var refreshToken = request.RefreshToken;
                if (string.IsNullOrWhiteSpace(refreshToken))
                {
                    Request.Cookies.TryGetValue("refreshToken", out refreshToken);
                }

                var result = await _authService.RefreshTokenAsync(refreshToken ?? string.Empty);

                return result.Match(
                    onSuccess: () =>
                    {
                        _authHttpContextService.ApplyRefreshTokenCookie(Response, result.Data);
                        return Ok(_authHttpContextService.CreateClientAuthResponse(HttpContext, result.Data));
                    },
                    onFailure: (error) =>
                    {
                        _authHttpContextService.ClearAuthCookies(Response);
                        return StatusCode(error.StatusCode ?? 401, new { success = false, message = error.Message, errors = error.Errors });
                    }
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refreshing token");
                return StatusCode(500, new { success = false, message = "An error occurred while refreshing token" });
            }
        }

        /// <summary>
        /// Verify user email with verification token
        /// </summary>
        [HttpPost("verify-email")]
        public async Task<ActionResult<AuthResponse>> VerifyEmail([FromBody] VerifyEmailRequest request)
        {
            try
            {
                var result = await _authService.VerifyEmailAsync(request);

                return result.Match(
                    onSuccess: () => Ok(result.Data),
                    onFailure: (error) => StatusCode(error.StatusCode ?? 400, new { success = false, message = error.Message, errors = error.Errors })
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verifying email");
                return StatusCode(500, new { success = false, message = "An error occurred while verifying email" });
            }
        }

        /// <summary>
        /// Resend verification email
        /// </summary>
        [HttpPost("resend-verification")]
        public async Task<ActionResult<AuthResponse>> ResendVerification([FromQuery] string email)
        {
            try
            {
                var result = await _authService.ResendVerificationEmailAsync(email);

                return result.Match(
                    onSuccess: () => Ok(result.Data),
                    onFailure: (error) => StatusCode(error.StatusCode ?? 400, new { success = false, message = error.Message, errors = error.Errors })
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resending verification email");
                return StatusCode(500, new { success = false, message = "An error occurred while resending verification email" });
            }
        }

        /// <summary>
        /// Request password reset
        /// </summary>
        [HttpPost("forgot-password")]
        public async Task<ActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
        {
            try
            {
                var result = await _authService.ForgotPasswordAsync(request);

                return result.Match(
                    onSuccess: () => Ok(new { success = true, message = result.Message }),
                    onFailure: (error) => StatusCode(error.StatusCode ?? 400, new { success = false, message = error.Message, errors = error.Errors })
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in forgot password");
                return StatusCode(500, new { success = false, message = "An error occurred while processing password reset request" });
            }
        }

        /// <summary>
        /// Reset password with reset token
        /// </summary>
        [HttpPost("reset-password")]
        public async Task<ActionResult<AuthResponse>> ResetPassword([FromBody] ResetPasswordRequest request)
        {
            try
            {
                var result = await _authService.ResetPasswordAsync(request);

                return result.Match(
                    onSuccess: () => Ok(result.Data),
                    onFailure: (error) => StatusCode(error.StatusCode ?? 400, new { success = false, message = error.Message, errors = error.Errors })
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resetting password");
                return StatusCode(500, new { success = false, message = "An error occurred while resetting password" });
            }
        }

        /// <summary>
        /// Google SSO Login
        /// </summary>
        [HttpPost("google-login")]
        [AllowAnonymous]
        [EnableRateLimiting("login")]
        public async Task<ActionResult<AuthResponse>> GoogleLogin([FromBody] GoogleLoginRequest request)
        {
            try
            {
                var result = await _authService.GoogleLoginAsync(_authHttpContextService.CreateGoogleLoginRequest(HttpContext, request));

                return result.Match(
                    onSuccess: () =>
                    {
                        _authHttpContextService.ApplyRefreshTokenCookie(Response, result.Data);
                        return Ok(_authHttpContextService.CreateClientAuthResponse(HttpContext, result.Data));
                    },
                    onFailure: (error) => StatusCode(error.StatusCode ?? 400, new { success = false, message = error.Message, errors = error.Errors })
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during Google login");
                return StatusCode(500, new { success = false, message = "An error occurred during Google login" });
            }
        }

        /// <summary>
        /// Logout user and revoke all tokens
        /// </summary>
        [HttpPost("logout")]
        [Authorize]
        public async Task<ActionResult> Logout()
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);

                if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
                    return Unauthorized(new { success = false, message = "User not authenticated" });

                var result = await _authService.LogoutAsync(userId);

                return result.Match(
                    onSuccess: () =>
                    {
                        _authHttpContextService.ClearAuthCookies(Response);
                        return Ok(new { success = true, message = result.Message });
                    },
                    onFailure: (error) => StatusCode(error.StatusCode ?? 500, new { success = false, message = error.Message, errors = error.Errors })
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during logout");
                return StatusCode(500, new { success = false, message = "An error occurred during logout" });
            }
        }

        /// <summary>
        /// Cleanup expired refresh tokens (admin/scheduled task)
        /// </summary>
        [HttpDelete("cleanup-tokens")]
        public async Task<ActionResult> CleanupTokens()
        {
            try
            {
                var result = await _authService.CleanupExpiredTokensAsync();

                return result.Match(
                    onSuccess: () => Ok(new { success = true, message = result.Message }),
                    onFailure: (error) => StatusCode(error.StatusCode ?? 500, new { success = false, message = error.Message, errors = error.Errors })
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during cleanup");
                return StatusCode(500, new { success = false, message = "An error occurred during cleanup" });
            }
        }

    }
}
