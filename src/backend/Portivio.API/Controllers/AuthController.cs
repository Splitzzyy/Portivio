using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Portivio.Application.DTOs.Auth;
using Portivio.Application.Results;
using Portivio.Application.Services;
using System.Security.Claims;

namespace Portivio.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;
        private readonly ILogger<AuthController> _logger;

        public AuthController(IAuthService authService, ILogger<AuthController> logger)
        {
            _authService = authService;
            _logger = logger;
        }

        /// <summary>
        /// Login user with email and password
        /// Only verified users can login. Generates access and refresh tokens.
        /// </summary>
        [HttpPost("login")]
        public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginRequest request)
        {
            try
            {
                var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
                var userAgent = HttpContext.Request.Headers.UserAgent.ToString();

                var loginRequest = new LoginRequest
                {
                    Email = request.Email,
                    Password = request.Password,
                    IpAddress = ipAddress,
                    DeviceInfo = userAgent
                };

                var result = await _authService.LoginAsync(loginRequest);

                return result.Match(
                    onSuccess: () =>
                    {
                        if (!string.IsNullOrEmpty(result.Data?.RefreshToken) && result.Data?.RefreshTokenExpiry.HasValue == true)
                        {
                            Response.Cookies.Append("refreshToken", result.Data.RefreshToken, new CookieOptions
                            {
                                HttpOnly = true,
                                Secure = true,
                                SameSite = SameSiteMode.Strict,
                                Expires = result.Data.RefreshTokenExpiry
                            });
                        }
                        return Ok(result.Data);
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
                var result = await _authService.RefreshTokenAsync(request.RefreshToken);

                return result.Match(
                    onSuccess: () =>
                    {
                        if (!string.IsNullOrEmpty(result.Data?.RefreshToken) && result.Data?.RefreshTokenExpiry.HasValue == true)
                        {
                            Response.Cookies.Append("refreshToken", result.Data.RefreshToken, new CookieOptions
                            {
                                HttpOnly = true,
                                Secure = true,
                                SameSite = SameSiteMode.Strict,
                                Expires = result.Data.RefreshTokenExpiry
                            });
                        }
                        return Ok(result.Data);
                    },
                    onFailure: (error) => StatusCode(error.StatusCode ?? 401, new { success = false, message = error.Message, errors = error.Errors })
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
        public async Task<ActionResult<AuthResponse>> GoogleLogin([FromBody] GoogleLoginRequest request)
        {
            try
            {
                var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
                var userAgent = HttpContext.Request.Headers.UserAgent.ToString();

                var googleRequest = new GoogleLoginRequest
                {
                    Token = request.Token,
                    IpAddress = ipAddress,
                    DeviceInfo = userAgent
                };

                var result = await _authService.GoogleLoginAsync(googleRequest);

                return result.Match(
                    onSuccess: () =>
                    {
                        if (!string.IsNullOrEmpty(result.Data?.RefreshToken) && result.Data?.RefreshTokenExpiry.HasValue == true)
                        {
                            Response.Cookies.Append("refreshToken", result.Data.RefreshToken, new CookieOptions
                            {
                                HttpOnly = true,
                                Secure = true,
                                SameSite = SameSiteMode.Strict,
                                Expires = result.Data.RefreshTokenExpiry
                            });
                        }
                        return Ok(result.Data);
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
                        Response.Cookies.Delete("refreshToken");
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
