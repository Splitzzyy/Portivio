using Google.Apis.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Portivio.Application.DTOs.Auth;
using Portivio.Application.Results;
using Portivio.Domain.Entities;
using Portivio.Infrastructure.Data;
using Portivio.Infrastructure.Services;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace Portivio.Application.Services
{
    public interface IAuthService
    {
        Task<Result<AuthResponse>> LoginAsync(LoginRequest request);
        Task<Result<AuthResponse>> SignupAsync(SignupRequest request);
        Task<Result<AuthResponse>> RefreshTokenAsync(string refreshToken);
        Task<Result<AuthResponse>> VerifyEmailAsync(VerifyEmailRequest request);
        Task<Result<AuthResponse>> ResendVerificationEmailAsync(string email);
        Task<Result<AuthResponse>> ForgotPasswordAsync(ForgotPasswordRequest request);
        Task<Result<AuthResponse>> ResetPasswordAsync(ResetPasswordRequest request);
        Task<Result<AuthResponse>> GoogleLoginAsync(GoogleLoginRequest request);
        Task<Result> LogoutAsync(Guid userId);
        Task<Result> CleanupExpiredTokensAsync();
    }

    public class AuthService : IAuthService
    {
        private readonly PortivioDbContext _context;
        private readonly AppSettingsOptions _jwtSettings;
        private readonly GoogleAuthOptions _googleAppSettings;
        private readonly ILogger<AuthService> _logger;
        private readonly IEmailJobService _emailJobService;

        public AuthService(
            PortivioDbContext context,
            IOptions<AppSettingsOptions> jwtOptions,
            IOptions<GoogleAuthOptions> googleOptions,
            ILogger<AuthService> logger,
            IEmailJobService emailJobService)
        {
            _context = context;
            _jwtSettings = jwtOptions.Value;
            _googleAppSettings = googleOptions.Value;
            _logger = logger;
            _emailJobService = emailJobService;
        }

        public async Task<Result<AuthResponse>> LoginAsync(LoginRequest request)
        {
            try
            {
                // Validate input
                if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
                {
                    _logger.LogWarning("Login failed: missing credentials. AuthEvent={AuthEvent} Outcome={Outcome} IpAddress={IpAddress}",
                        "Login", "BadRequest", request.IpAddress);
                    return Result<AuthResponse>.BadRequest("Email and password are required");
                }

                var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == request.Email);

                if (user == null)
                {
                    _logger.LogWarning("Login failed: invalid credentials (user not found). AuthEvent={AuthEvent} Outcome={Outcome} Email={Email} IpAddress={IpAddress}",
                        "Login", "Unauthorized", request.Email, request.IpAddress);
                    return Result<AuthResponse>.Unauthorized("Invalid credentials");
                }

                if (!user.IsVerified)
                {
                    _logger.LogWarning("Login failed: email not verified. AuthEvent={AuthEvent} Outcome={Outcome} Email={Email} UserId={UserId} IpAddress={IpAddress}",
                        "Login", "BadRequest", request.Email, user.Id, request.IpAddress);
                    return Result<AuthResponse>.BadRequest("Email not verified. Please verify your email to login.");
                }

                if (!user.IsActive)
                {
                    _logger.LogWarning("Login failed: account is inactive. AuthEvent={AuthEvent} Outcome={Outcome} Email={Email} UserId={UserId} IpAddress={IpAddress}",
                        "Login", "Forbidden", request.Email, user.Id, request.IpAddress);
                    return Result<AuthResponse>.Forbidden("Account is inactive");
                }

                if (!VerifyPassword(request.Password, user.PasswordHash))
                {
                    _logger.LogWarning("Login failed: invalid credentials (wrong password). AuthEvent={AuthEvent} Outcome={Outcome} Email={Email} UserId={UserId} IpAddress={IpAddress}",
                        "Login", "Unauthorized", request.Email, user.Id, request.IpAddress);
                    return Result<AuthResponse>.Unauthorized("Invalid credentials");
                }

                user.LastLoginAt = DateTime.UtcNow;
                _context.Users.Update(user);

                var tokensResult = await GenerateTokensAsync(
                    user,
                    request.IpAddress ?? "Unknown",
                    request.DeviceInfo ?? "Unknown",
                    request.IssueRefreshToken);
                
                if (tokensResult.IsFailure)
                {
                    _logger.LogError("Login failed: token generation error. AuthEvent={AuthEvent} Outcome={Outcome} Email={Email} UserId={UserId} Error={Error}",
                        "Login", "Failure", request.Email, user.Id, tokensResult.Message);
                    return Result<AuthResponse>.Failure(tokensResult.Message, tokensResult.Errors, tokensResult.StatusCode ?? 500);
                }

                await _context.SaveChangesAsync();

                _logger.LogInformation("Login successful. AuthEvent={AuthEvent} Outcome={Outcome} Email={Email} UserId={UserId} IpAddress={IpAddress} DeviceInfo={DeviceInfo} IssueRefreshToken={IssueRefreshToken}",
                    "Login", "Success", request.Email, user.Id, request.IpAddress, request.DeviceInfo, request.IssueRefreshToken);

                var response = new AuthResponse
                {
                    Success = true,
                    Message = "Login successful",
                    AccessToken = tokensResult.Data!.AccessToken,
                    RefreshToken = tokensResult.Data!.RefreshToken,
                    AccessTokenExpiry = tokensResult.Data!.AccessTokenExpiry,
                    RefreshTokenExpiry = tokensResult.Data!.RefreshTokenExpiry,
                    User = new UserDto
                    {
                        Id = user.Id,
                        Email = user.Email,
                        Name = user.Name,
                        IsVerified = user.IsVerified,
                        IsActive = user.IsActive
                    }
                };

                return Result<AuthResponse>.Success(response, "Login successful", 200);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Login failed: unexpected exception. AuthEvent={AuthEvent} Outcome={Outcome} Email={Email} IpAddress={IpAddress}",
                    "Login", "InternalServerError", request.Email, request.IpAddress);
                return Result<AuthResponse>.InternalServerError($"Login failed: {ex.Message}");
            }
        }

        public async Task<Result<AuthResponse>> SignupAsync(SignupRequest request)
        {
            try
            {
                // Validate input
                var validationErrors = new List<string>();

                if (string.IsNullOrWhiteSpace(request.Email))
                    validationErrors.Add("Email is required");
                if (string.IsNullOrWhiteSpace(request.Name))
                    validationErrors.Add("Name is required");
                if (string.IsNullOrWhiteSpace(request.Password))
                    validationErrors.Add("Password is required");

                if (validationErrors.Any())
                    return Result<AuthResponse>.BadRequest(string.Join(", ", validationErrors));

                if (request.Password != request.ConfirmPassword)
                    return Result<AuthResponse>.BadRequest("Passwords do not match");

                var existingUser = await _context.Users.FirstOrDefaultAsync(u => u.Email == request.Email);
                if (existingUser != null)
                    return Result<AuthResponse>.Conflict("Email already registered");

                var verificationToken = GenerateSecureToken();

                var user = new User
                {
                    Id = Guid.NewGuid(),
                    Email = request.Email,
                    Name = request.Name,
                    PasswordHash = HashPassword(request.Password),
                    IsVerified = false,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    EmailVerificationToken = verificationToken,
                    EmailVerificationTokenExpiry = DateTime.UtcNow.AddHours(24)
                };

                _context.Users.Add(user);
                await _context.SaveChangesAsync();

                _emailJobService.EnqueueVerificationEmail(user.Email, user.Name, verificationToken);
                _emailJobService.EnqueueWelcomeEmail(user.Email, user.Name);

                var response = new AuthResponse
                {
                    Success = true,
                    Message = "Signup successful. Please verify your email.",
                    User = new UserDto
                    {
                        Id = user.Id,
                        Email = user.Email,
                        Name = user.Name,
                        IsVerified = user.IsVerified,
                        IsActive = user.IsActive
                    }
                };

                return Result<AuthResponse>.Success(response, "Signup successful", 201);
            }
            catch (Exception ex)
            {
                return Result<AuthResponse>.InternalServerError($"Signup failed: {ex.Message}");
            }
        }

        public async Task<Result<AuthResponse>> RefreshTokenAsync(string refreshToken)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(refreshToken))
                    return Result<AuthResponse>.BadRequest("Refresh token is required");

                var hashedToken = HashToken(refreshToken);
                var authToken = await _context.AuthTokens
                    .Include(at => at.User)
                    .FirstOrDefaultAsync(at => at.RefreshTokenHash == hashedToken && !at.Revoked);

                if (authToken == null)
                    return Result<AuthResponse>.Unauthorized("Invalid refresh token");

                if (authToken.RefreshTokenExpiry < DateTime.UtcNow)
                    return Result<AuthResponse>.Unauthorized("Refresh token has expired");

                var user = authToken.User;
                if (!user.IsActive)
                    return Result<AuthResponse>.Forbidden("Account is inactive");

                if (!user.IsVerified)
                    return Result<AuthResponse>.Unauthorized("User is not eligible for token refresh");

                var tokensResult = await GenerateTokensAsync(user, authToken.IpAddress, authToken.DeviceInfo, issueRefreshToken: true);

                if (tokensResult.IsFailure)
                    return Result<AuthResponse>.Failure(tokensResult.Message, tokensResult.Errors, tokensResult.StatusCode ?? 500);

                // Revoke old token
                authToken.Revoked = true;
                _context.AuthTokens.Update(authToken);
                await _context.SaveChangesAsync();

                var response = new AuthResponse
                {
                    Success = true,
                    Message = "Token refreshed successfully",
                    AccessToken = tokensResult.Data!.AccessToken,
                    RefreshToken = tokensResult.Data!.RefreshToken,
                    AccessTokenExpiry = tokensResult.Data!.AccessTokenExpiry,
                    RefreshTokenExpiry = tokensResult.Data!.RefreshTokenExpiry
                };

                return Result<AuthResponse>.Success(response, "Token refreshed successfully", 200);
            }
            catch (Exception ex)
            {
                return Result<AuthResponse>.InternalServerError($"Token refresh failed: {ex.Message}");
            }
        }

        public async Task<Result<AuthResponse>> VerifyEmailAsync(VerifyEmailRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.VerificationToken))
                    return Result<AuthResponse>.BadRequest("Email and verification token are required");

                var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == request.Email);

                if (user == null)
                    return Result<AuthResponse>.NotFound("User not found");

                if (user.IsVerified)
                    return Result<AuthResponse>.BadRequest("Email is already verified");

                if (user.EmailVerificationToken == null
                    || user.EmailVerificationToken != request.VerificationToken
                    || user.EmailVerificationTokenExpiry < DateTime.UtcNow)
                    return Result<AuthResponse>.BadRequest("Invalid or expired verification token");

                user.IsVerified = true;
                user.EmailVerificationToken = null;
                user.EmailVerificationTokenExpiry = null;
                _context.Users.Update(user);
                await _context.SaveChangesAsync();

                var response = new AuthResponse
                {
                    Success = true,
                    Message = "Email verified successfully",
                    User = new UserDto
                    {
                        Id = user.Id,
                        Email = user.Email,
                        Name = user.Name,
                        IsVerified = user.IsVerified,
                        IsActive = user.IsActive
                    }
                };

                return Result<AuthResponse>.Success(response, "Email verified successfully", 200);
            }
            catch (Exception ex)
            {
                return Result<AuthResponse>.InternalServerError($"Email verification failed: {ex.Message}");
            }
        }

        public async Task<Result<AuthResponse>> ResendVerificationEmailAsync(string email)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(email))
                    return Result<AuthResponse>.BadRequest("Email is required");

                var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);

                if (user == null)
                    return Result<AuthResponse>.NotFound("User not found");

                if (user.IsVerified)
                    return Result<AuthResponse>.BadRequest("Email is already verified");

                var verificationToken = GenerateSecureToken();
                user.EmailVerificationToken = verificationToken;
                user.EmailVerificationTokenExpiry = DateTime.UtcNow.AddHours(24);
                _context.Users.Update(user);
                await _context.SaveChangesAsync();

                _emailJobService.EnqueueVerificationEmail(user.Email, user.Name, verificationToken);

                var response = new AuthResponse
                {
                    Success = true,
                    Message = "Verification email sent"
                };

                return Result<AuthResponse>.Success(response, "Verification email sent", 200);
            }
            catch (Exception ex)
            {
                return Result<AuthResponse>.InternalServerError($"Resend verification failed: {ex.Message}");
            }
        }

        public async Task<Result<AuthResponse>> ForgotPasswordAsync(ForgotPasswordRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.Email))
                    return Result<AuthResponse>.BadRequest("Email is required");

                var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == request.Email);

                if (user == null)
                    return Result<AuthResponse>.NotFound("User not found");

                var resetToken = GenerateSecureToken();
                user.PasswordResetToken = resetToken;
                user.PasswordResetTokenExpiry = DateTime.UtcNow.AddHours(1);
                _context.Users.Update(user);
                await _context.SaveChangesAsync();

                _emailJobService.EnqueuePasswordResetEmail(user.Email, user.Name, resetToken);

                var response = new AuthResponse
                {
                    Success = true,
                    Message = "If the email exists, a reset link will be sent"
                };

                return Result<AuthResponse>.Success(response, "Password reset request processed", 200);
            }
            catch (Exception ex)
            {
                return Result<AuthResponse>.InternalServerError($"Forgot password failed: {ex.Message}");
            }
        }

        public async Task<Result<AuthResponse>> ResetPasswordAsync(ResetPasswordRequest request)
        {
            try
            {
                var validationErrors = new List<string>();

                if (string.IsNullOrWhiteSpace(request.Email))
                    validationErrors.Add("Email is required");
                if (string.IsNullOrWhiteSpace(request.NewPassword))
                    validationErrors.Add("New password is required");
                if (string.IsNullOrWhiteSpace(request.ResetToken))
                    validationErrors.Add("Reset token is required");

                if (validationErrors.Any())
                    return Result<AuthResponse>.BadRequest(string.Join(", ", validationErrors));

                if (request.NewPassword != request.ConfirmPassword)
                    return Result<AuthResponse>.BadRequest("Passwords do not match");

                var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == request.Email);

                if (user == null)
                    return Result<AuthResponse>.NotFound("User not found");

                if (user.PasswordResetToken == null
                    || user.PasswordResetToken != request.ResetToken
                    || user.PasswordResetTokenExpiry < DateTime.UtcNow)
                    return Result<AuthResponse>.BadRequest("Invalid or expired reset token");

                user.PasswordHash = HashPassword(request.NewPassword);
                user.IsActive = true;
                user.PasswordResetToken = null;
                user.PasswordResetTokenExpiry = null;
                _context.Users.Update(user);
                await _context.SaveChangesAsync();

                var response = new AuthResponse
                {
                    Success = true,
                    Message = "Password reset successfully"
                };

                return Result<AuthResponse>.Success(response, "Password reset successfully", 200);
            }
            catch (Exception ex)
            {
                return Result<AuthResponse>.InternalServerError($"Password reset failed: {ex.Message}");
            }
        }

        public async Task<Result<AuthResponse>> GoogleLoginAsync(GoogleLoginRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.Token))
                {
                    _logger.LogWarning("Google login rejected: missing token. IP={IpAddress}", request.IpAddress);
                    return Result<AuthResponse>.BadRequest("Google token is required");
                }

                if (string.IsNullOrWhiteSpace(_googleAppSettings.ClientId))
                {
                    _logger.LogCritical("Google login cannot proceed: GoogleAuth:ClientId is not configured");
                    return Result<AuthResponse>.InternalServerError("Google Client ID is not configured");
                }

                var validAudiences = new List<string> { _googleAppSettings.ClientId };
                if (!string.IsNullOrWhiteSpace(_googleAppSettings.AndroidClientId))
                    validAudiences.Add(_googleAppSettings.AndroidClientId);

                GoogleJsonWebSignature.Payload payload;
                try
                {
                    payload = await GoogleJsonWebSignature.ValidateAsync(
                        request.Token,
                        new GoogleJsonWebSignature.ValidationSettings { Audience = validAudiences });
                }
                catch (InvalidJwtException ex)
                {
                    _logger.LogWarning("Google token validation failed. IP={IpAddress} Reason={Reason}",
                        request.IpAddress, ex.Message);
                    return Result<AuthResponse>.Unauthorized("Invalid or expired Google token");
                }

                _logger.LogInformation("Google token validated. Email={Email} IP={IpAddress} Device={DeviceInfo}",
                    payload.Email, request.IpAddress, request.DeviceInfo);

                if (!payload.EmailVerified)
                {
                    _logger.LogWarning("Google login rejected: email not verified. Email={Email} IP={IpAddress}",
                        payload.Email, request.IpAddress);
                    return Result<AuthResponse>.Unauthorized("Google account email is not verified");
                }

                var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == payload.Email);
                var isNewUser = user == null;

                if (isNewUser)
                {
                    user = new User
                    {
                        Id = Guid.NewGuid(),
                        Email = payload.Email,
                        Name = payload.Name,
                        PasswordHash = HashPassword(Guid.NewGuid().ToString()),
                        IsVerified = true,
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow
                    };

                    _context.Users.Add(user);
                    _emailJobService.EnqueueWelcomeEmail(user.Email, user.Name);
                }

                user!.LastLoginAt = DateTime.UtcNow;

                var tokensResult = await GenerateTokensAsync(
                    user,
                    request.IpAddress ?? "Unknown",
                    request.DeviceInfo ?? "Unknown",
                    true);

                if (tokensResult.IsFailure)
                {
                    _logger.LogError("Token generation failed for Google login. UserId={UserId} Error={Error}",
                        user!.Id, tokensResult.Message);
                    return Result<AuthResponse>.Failure(tokensResult.Message, tokensResult.Errors, tokensResult.StatusCode ?? 500);
                }

                // Saves both new user (if any) and auth token atomically in a single implicit transaction
                await _context.SaveChangesAsync();

                if (isNewUser)
                    _logger.LogInformation("New user registered via Google SSO. UserId={UserId} Email={Email} IP={IpAddress}",
                        user!.Id, user.Email, request.IpAddress);

                _logger.LogInformation("Google SSO login successful. UserId={UserId} Email={Email} IP={IpAddress} Device={DeviceInfo}",
                    user!.Id, user.Email, request.IpAddress, request.DeviceInfo);

                var response = new AuthResponse
                {
                    Success = true,
                    Message = "Login successful",
                    AccessToken = tokensResult.Data!.AccessToken,
                    RefreshToken = tokensResult.Data!.RefreshToken,
                    AccessTokenExpiry = tokensResult.Data!.AccessTokenExpiry,
                    RefreshTokenExpiry = tokensResult.Data!.RefreshTokenExpiry,
                    User = new UserDto
                    {
                        Id = user.Id,
                        Email = user.Email,
                        Name = user.Name,
                        IsVerified = user.IsVerified,
                        IsActive = user.IsActive
                    }
                };

                return Result<AuthResponse>.Success(response, "Login successful", 200);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled error during Google SSO login. IP={IpAddress} Device={DeviceInfo}",
                    request.IpAddress, request.DeviceInfo);
                return Result<AuthResponse>.InternalServerError($"Google login failed: {ex.Message}");
            }
        }

        public async Task<Result> LogoutAsync(Guid userId)
        {
            try
            {
                var tokens = await _context.AuthTokens.Where(at => at.UserId == userId && !at.Revoked)
                    .ToListAsync();

                if (!tokens.Any())
                    return Result.Success("Logout successful", 200);

                foreach (var token in tokens)
                {
                    token.Revoked = true;
                }

                _context.AuthTokens.UpdateRange(tokens);
                await _context.SaveChangesAsync();

                return Result.Success("Logout successful", 200);
            }
            catch (Exception ex)
            {
                return Result.InternalServerError($"Logout failed: {ex.Message}");
            }
        }

        public async Task<Result> CleanupExpiredTokensAsync()
        {
            try
            {
                var expiredTokens = await _context.AuthTokens
                    .Where(at => at.RefreshTokenExpiry < DateTime.UtcNow)
                    .ToListAsync();

                if (expiredTokens.Any())
                {
                    _context.AuthTokens.RemoveRange(expiredTokens);
                    await _context.SaveChangesAsync();
                }

                return Result.Success($"Cleanup completed. Removed {expiredTokens.Count} expired tokens", 200);
            }
            catch (Exception ex)
            {
                return Result.InternalServerError($"Cleanup failed: {ex.Message}");
            }
        }

        private async Task<Result<TokenData>> GenerateTokensAsync(User user, string ipAddress, string deviceInfo, bool issueRefreshToken)
        {
            try
            {
                var accessTokenExpiry = DateTime.UtcNow.AddHours(1);

                var accessTokenResult = GenerateJwtToken(user, accessTokenExpiry);
                if (accessTokenResult.IsFailure)
                    return Result<TokenData>.Failure(accessTokenResult.Message, accessTokenResult.Errors, 500);

                string? refreshToken = null;
                DateTime? refreshTokenExpiry = null;

                if (issueRefreshToken)
                {
                    refreshToken = GenerateRefreshToken();
                    refreshTokenExpiry = DateTime.UtcNow.AddDays(7);

                    var authToken = new AuthToken
                    {
                        Id = Guid.NewGuid(),
                        UserId = user.Id,
                        AccessTokenHash = HashToken(accessTokenResult.Data ?? string.Empty),
                        RefreshTokenHash = HashToken(refreshToken),
                        AccessTokenExpiry = accessTokenExpiry,
                        RefreshTokenExpiry = refreshTokenExpiry.Value,
                        DeviceInfo = deviceInfo,
                        IpAddress = ipAddress,
                        Revoked = false,
                        CreatedAt = DateTime.UtcNow
                    };

                    _context.AuthTokens.Add(authToken);
                }

                var tokenData = new TokenData
                {
                    AccessToken = accessTokenResult.Data ?? string.Empty,
                    RefreshToken = refreshToken,
                    AccessTokenExpiry = accessTokenExpiry,
                    RefreshTokenExpiry = refreshTokenExpiry
                };

                return Result<TokenData>.Success(tokenData, "Tokens generated successfully", 200);
            }
            catch (Exception ex)
            {
                return Result<TokenData>.InternalServerError($"Token generation failed: {ex.Message}");
            }
        }

        private Result<string> GenerateJwtToken(User user, DateTime expiry)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(_jwtSettings.Key))
                    throw new InvalidOperationException("JWT Key is not configured");

                var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.Key));
                var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

                var claims = new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                    new Claim(ClaimTypes.Email, user.Email),
                    new Claim(ClaimTypes.Name, user.Name)
                };

                var token = new JwtSecurityToken(
                    issuer: _jwtSettings.Issuer,
                    audience: _jwtSettings.Audience,
                    claims: claims,
                    expires: expiry,
                    signingCredentials: credentials
                );

                var tokenString = new JwtSecurityTokenHandler().WriteToken(token);
                return Result<string>.Success(tokenString, "JWT token generated successfully", 200);
            }
            catch (Exception ex)
            {
                return Result<string>.InternalServerError($"JWT generation failed: {ex.Message}");
            }
        }

        private string GenerateRefreshToken()
        {
            var randomNumber = new byte[64];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(randomNumber);
            }
            return Convert.ToBase64String(randomNumber);
        }

        private static string GenerateSecureToken()
        {
            var bytes = new byte[32];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(bytes);
            return Convert.ToBase64String(bytes)
                .Replace('+', '-')
                .Replace('/', '_')
                .TrimEnd('=');
        }

        private string HashToken(string token)
        {
            using (var sha256 = SHA256.Create())
            {
                var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(token));
                return Convert.ToBase64String(hashedBytes);
            }
        }

        private static string HashPassword(string password)
        {
            return BCrypt.Net.BCrypt.HashPassword(password);
        }

        private static bool VerifyPassword(string password, string? passwordHash)
        {
            if (string.IsNullOrWhiteSpace(passwordHash))
            {
                return false;
            }

            return BCrypt.Net.BCrypt.Verify(password, passwordHash);
        }
    }

    public class TokenData
    {
        public string AccessToken { get; set; } = null!;
        public string? RefreshToken { get; set; }
        public DateTime AccessTokenExpiry { get; set; }
        public DateTime? RefreshTokenExpiry { get; set; }
    }
}
