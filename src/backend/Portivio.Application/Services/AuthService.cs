using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Portivio.Application.DTOs.Auth;
using Portivio.Application.Results;
using Portivio.Domain.Entities;
using Portivio.Infrastructure.Data;
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
        private readonly IConfiguration _configuration;

        public AuthService(PortivioDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        public async Task<Result<AuthResponse>> LoginAsync(LoginRequest request)
        {
            try
            {
                // Validate input
                if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
                    return Result<AuthResponse>.BadRequest("Email and password are required");

                var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == request.Email);

                if (user == null)
                    return Result<AuthResponse>.Unauthorized("Invalid credentials");

                if (!user.IsVerified)
                    return Result<AuthResponse>.BadRequest("Email not verified. Please verify your email to login.");

                if (!user.IsActive)
                    return Result<AuthResponse>.Forbidden("Account is inactive");

                // NOTE: In production, use a proper password hashing service (bcrypt, Argon2, etc.)
                if (!VerifyPassword(request.Password, user.Email))
                    return Result<AuthResponse>.Unauthorized("Invalid credentials");

                user.LastLoginAt = DateTime.UtcNow;
                _context.Users.Update(user);

                var tokensResult = await GenerateTokensAsync(user, request.IpAddress ?? "Unknown", request.DeviceInfo ?? "Unknown");
                if (tokensResult.IsFailure)
                    return Result<AuthResponse>.Failure(tokensResult.Message, tokensResult.Errors, tokensResult.StatusCode ?? 500);

                await _context.SaveChangesAsync();

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

                var user = new User
                {
                    Id = Guid.NewGuid(),
                    Email = request.Email,
                    Name = request.Name,
                    IsVerified = false,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };

                _context.Users.Add(user);
                await _context.SaveChangesAsync();

                // TODO: Send verification email here

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
                var tokensResult = await GenerateTokensAsync(user, authToken.IpAddress, authToken.DeviceInfo);

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

                // TODO: Verify the token (should be stored separately in a EmailVerification table)
                var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == request.Email);

                if (user == null)
                    return Result<AuthResponse>.NotFound("User not found");

                if (user.IsVerified)
                    return Result<AuthResponse>.BadRequest("Email is already verified");

                user.IsVerified = true;
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

                // TODO: Generate and send verification email

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

                // TODO: Generate reset token and send email

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

                // TODO: Validate reset token

                user.IsActive = true;
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
                    return Result<AuthResponse>.BadRequest("Google token is required");

                // TODO: Verify Google token and extract email/profile info

                return Result<AuthResponse>.Failure("Google login not fully implemented", 501);
            }
            catch (Exception ex)
            {
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

        private async Task<Result<TokenData>> GenerateTokensAsync(User user, string ipAddress, string deviceInfo)
        {
            try
            {
                var accessTokenExpiry = DateTime.UtcNow.AddHours(1);
                var refreshTokenExpiry = DateTime.UtcNow.AddDays(7);

                var accessTokenResult = GenerateJwtToken(user, accessTokenExpiry);
                if (accessTokenResult.IsFailure)
                    return Result<TokenData>.Failure(accessTokenResult.Message, accessTokenResult.Errors, 500);

                var refreshToken = GenerateRefreshToken();

                var authToken = new AuthToken
                {
                    Id = Guid.NewGuid(),
                    UserId = user.Id,
                    AccessTokenHash = HashToken(accessTokenResult.Data ?? ""),
                    RefreshTokenHash = HashToken(refreshToken),
                    AccessTokenExpiry = accessTokenExpiry,
                    RefreshTokenExpiry = refreshTokenExpiry,
                    DeviceInfo = deviceInfo,
                    IpAddress = ipAddress,
                    Revoked = false,
                    CreatedAt = DateTime.UtcNow
                };

                _context.AuthTokens.Add(authToken);

                var tokenData = new TokenData
                {
                    AccessToken = accessTokenResult.Data ?? "",
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
                var jwtKey = _configuration["Jwt:Key"] ?? throw new InvalidOperationException("JWT Key is not configured");
                var jwtIssuer = _configuration["Jwt:Issuer"];
                var jwtAudience = _configuration["Jwt:Audience"];

                var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
                var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

                var claims = new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                    new Claim(ClaimTypes.Email, user.Email),
                    new Claim(ClaimTypes.Name, user.Name)
                };

                var token = new JwtSecurityToken(
                    issuer: jwtIssuer,
                    audience: jwtAudience,
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

        private string HashToken(string token)
        {
            using (var sha256 = SHA256.Create())
            {
                var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(token));
                return Convert.ToBase64String(hashedBytes);
            }
        }

        private bool VerifyPassword(string password, string email)
        {
            // TODO: Implement proper password verification with bcrypt or similar
            return true;
        }
    }

    public class TokenData
    {
        public string AccessToken { get; set; } = null!;
        public string RefreshToken { get; set; } = null!;
        public DateTime AccessTokenExpiry { get; set; }
        public DateTime RefreshTokenExpiry { get; set; }
    }
}
