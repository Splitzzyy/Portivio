using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;
using Portivio.Application.DTOs.Auth;
using Portivio.Application.Services;
using Portivio.Domain.Entities;
using Portivio.Infrastructure.Data;
using Xunit;

namespace Portivio.Tests.Services
{
    public class AuthServiceLoginTests
    {
        private PortivioDbContext CreateInMemoryDbContext()
        {
            var options = new DbContextOptionsBuilder<PortivioDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            return new PortivioDbContext(options);
        }

        private Mock<IConfiguration> CreateMockConfiguration()
        {
            var config = new Mock<IConfiguration>();
            config.Setup(c => c["Jwt:Key"])
                .Returns("super-secret-key-that-is-at-least-32-characters-long");
            config.Setup(c => c["Jwt:Issuer"])
                .Returns("Portivio");
            config.Setup(c => c["Jwt:Audience"])
                .Returns("PortivioUsers");
            return config;
        }

        [Fact]
        public async Task LoginAsync_WithValidCredentials_ReturnsSuccessResult()
        {
            // Arrange
            var context = CreateInMemoryDbContext();
            var configMock = CreateMockConfiguration();

            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = "test@example.com",
                Name = "Test User",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Password123"),
                IsVerified = true,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            context.Users.Add(user);
            await context.SaveChangesAsync();

            var service = new AuthService(context, configMock.Object);
            var request = new LoginRequest
            {
                Email = "test@example.com",
                Password = "Password123",
                IssueRefreshToken = false
            };

            // Act
            var result = await service.LoginAsync(request);

            // Assert
            Assert.True(result.IsSuccess);
            Assert.NotNull(result.Data);
            Assert.Equal("Login successful", result.Message);
            Assert.NotNull(result.Data.AccessToken);
            Assert.Null(result.Data.RefreshToken);
        }

        [Fact]
        public async Task LoginAsync_WithPhoneClient_ReturnsRefreshToken()
        {
            var context = CreateInMemoryDbContext();
            var configMock = CreateMockConfiguration();

            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = "phone@example.com",
                Name = "Phone User",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Password123"),
                IsVerified = true,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            context.Users.Add(user);
            await context.SaveChangesAsync();

            var service = new AuthService(context, configMock.Object);
            var request = new LoginRequest
            {
                Email = "phone@example.com",
                Password = "Password123",
                DeviceInfo = "Mozilla/5.0 (iPhone; CPU iPhone OS 17_0 like Mac OS X) Mobile",
                IssueRefreshToken = true
            };

            var result = await service.LoginAsync(request);

            Assert.True(result.IsSuccess);
            Assert.NotNull(result.Data?.AccessToken);
            Assert.NotNull(result.Data?.RefreshToken);
        }

        [Fact]
        public async Task LoginAsync_WithoutRefreshToken_DoesNotPersistAuthToken()
        {
            var context = CreateInMemoryDbContext();
            var configMock = CreateMockConfiguration();

            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = "desktop@example.com",
                Name = "Desktop User",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Password123"),
                IsVerified = true,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            context.Users.Add(user);
            await context.SaveChangesAsync();

            var service = new AuthService(context, configMock.Object);
            var request = new LoginRequest
            {
                Email = "desktop@example.com",
                Password = "Password123",
                DeviceInfo = "Mozilla/5.0 (Windows NT 10.0; Win64; x64)",
                IssueRefreshToken = false
            };

            var result = await service.LoginAsync(request);

            Assert.True(result.IsSuccess);
            Assert.Empty(await context.AuthTokens.Where(t => t.UserId == user.Id).ToListAsync());
        }

        [Fact]
        public async Task LoginAsync_WithInvalidEmail_ReturnsUnauthorized()
        {
            // Arrange
            var context = CreateInMemoryDbContext();
            var configMock = CreateMockConfiguration();
            var service = new AuthService(context, configMock.Object);

            var request = new LoginRequest
            {
                Email = "nonexistent@example.com",
                Password = "Password123"
            };

            // Act
            var result = await service.LoginAsync(request);

            // Assert
            Assert.True(result.IsFailure);
            Assert.Equal(401, result.StatusCode);
            Assert.Equal("Invalid credentials", result.Message);
        }

        [Fact]
        public async Task LoginAsync_WithUnverifiedEmail_ReturnsBadRequest()
        {
            // Arrange
            var context = CreateInMemoryDbContext();
            var configMock = CreateMockConfiguration();

            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = "unverified@example.com",
                Name = "Unverified User",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Password123"),
                IsVerified = false,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            context.Users.Add(user);
            await context.SaveChangesAsync();

            var service = new AuthService(context, configMock.Object);
            var request = new LoginRequest
            {
                Email = "unverified@example.com",
                Password = "Password123"
            };

            // Act
            var result = await service.LoginAsync(request);

            // Assert
            Assert.True(result.IsFailure);
            Assert.Equal(400, result.StatusCode);
            Assert.Contains("Email not verified", result.Message);
        }

        [Fact]
        public async Task LoginAsync_WithInactiveAccount_ReturnsForbidden()
        {
            // Arrange
            var context = CreateInMemoryDbContext();
            var configMock = CreateMockConfiguration();

            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = "inactive@example.com",
                Name = "Inactive User",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Password123"),
                IsVerified = true,
                IsActive = false,
                CreatedAt = DateTime.UtcNow
            };

            context.Users.Add(user);
            await context.SaveChangesAsync();

            var service = new AuthService(context, configMock.Object);
            var request = new LoginRequest
            {
                Email = "inactive@example.com",
                Password = "Password123"
            };

            // Act
            var result = await service.LoginAsync(request);

            // Assert
            Assert.True(result.IsFailure);
            Assert.Equal(403, result.StatusCode);
            Assert.Equal("Account is inactive", result.Message);
        }

        [Fact]
        public async Task LoginAsync_WithEmptyCredentials_ReturnsBadRequest()
        {
            // Arrange
            var context = CreateInMemoryDbContext();
            var configMock = CreateMockConfiguration();
            var service = new AuthService(context, configMock.Object);

            var request = new LoginRequest
            {
                Email = "",
                Password = ""
            };

            // Act
            var result = await service.LoginAsync(request);

            // Assert
            Assert.True(result.IsFailure);
            Assert.Equal(400, result.StatusCode);
        }

        [Fact]
        public async Task LoginAsync_UpdatesLastLoginTime()
        {
            // Arrange
            var context = CreateInMemoryDbContext();
            var configMock = CreateMockConfiguration();

            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = "test@example.com",
                Name = "Test User",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Password123"),
                IsVerified = true,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                LastLoginAt = null
            };

            context.Users.Add(user);
            await context.SaveChangesAsync();

            var service = new AuthService(context, configMock.Object);
            var request = new LoginRequest
            {
                Email = "test@example.com",
                Password = "Password123",
                IssueRefreshToken = false
            };

            // Act
            await service.LoginAsync(request);
            var updatedUser = await context.Users.FirstOrDefaultAsync(u => u.Id == user.Id);

            // Assert
            Assert.NotNull(updatedUser?.LastLoginAt);
            Assert.True(updatedUser.LastLoginAt > DateTime.UtcNow.AddSeconds(-5));
        }
    }

    public class AuthServiceSignupTests
    {
        private PortivioDbContext CreateInMemoryDbContext()
        {
            var options = new DbContextOptionsBuilder<PortivioDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            return new PortivioDbContext(options);
        }

        private Mock<IConfiguration> CreateMockConfiguration()
        {
            var config = new Mock<IConfiguration>();
            config.Setup(c => c["Jwt:Key"])
                .Returns("super-secret-key-that-is-at-least-32-characters-long");
            config.Setup(c => c["Jwt:Issuer"])
                .Returns("Portivio");
            config.Setup(c => c["Jwt:Audience"])
                .Returns("PortivioUsers");
            return config;
        }

        [Fact]
        public async Task SignupAsync_WithValidData_CreatesUserSuccessfully()
        {
            // Arrange
            var context = CreateInMemoryDbContext();
            var configMock = CreateMockConfiguration();
            var service = new AuthService(context, configMock.Object);

            var request = new SignupRequest
            {
                Email = "newuser@example.com",
                Name = "New User",
                Password = "Password123",
                ConfirmPassword = "Password123"
            };

            // Act
            var result = await service.SignupAsync(request);

            // Assert
            Assert.True(result.IsSuccess);
            Assert.NotNull(result.Data);
            Assert.Equal(201, result.StatusCode);
            Assert.Equal("Signup successful", result.Message);

            var createdUser = await context.Users.FirstOrDefaultAsync(u => u.Email == "newuser@example.com");
            Assert.NotNull(createdUser);
            Assert.False(createdUser.IsVerified);
            Assert.True(createdUser.IsActive);
            Assert.False(string.IsNullOrWhiteSpace(createdUser.PasswordHash));
            Assert.NotEqual("Password123", createdUser.PasswordHash);
            Assert.True(BCrypt.Net.BCrypt.Verify("Password123", createdUser.PasswordHash));
        }

        [Fact]
        public async Task SignupAsync_WithPasswordMismatch_ReturnsBadRequest()
        {
            // Arrange
            var context = CreateInMemoryDbContext();
            var configMock = CreateMockConfiguration();
            var service = new AuthService(context, configMock.Object);

            var request = new SignupRequest
            {
                Email = "test@example.com",
                Name = "Test User",
                Password = "Password123",
                ConfirmPassword = "DifferentPassword"
            };

            // Act
            var result = await service.SignupAsync(request);

            // Assert
            Assert.True(result.IsFailure);
            Assert.Equal(400, result.StatusCode);
            Assert.Contains("do not match", result.Message);
        }

        [Fact]
        public async Task SignupAsync_WithExistingEmail_ReturnsConflict()
        {
            // Arrange
            var context = CreateInMemoryDbContext();
            var configMock = CreateMockConfiguration();

            var existingUser = new User
            {
                Id = Guid.NewGuid(),
                Email = "existing@example.com",
                Name = "Existing User",
                IsVerified = true,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            context.Users.Add(existingUser);
            await context.SaveChangesAsync();

            var service = new AuthService(context, configMock.Object);
            var request = new SignupRequest
            {
                Email = "existing@example.com",
                Name = "New User",
                Password = "Password123",
                ConfirmPassword = "Password123"
            };

            // Act
            var result = await service.SignupAsync(request);

            // Assert
            Assert.True(result.IsFailure);
            Assert.Equal(409, result.StatusCode);
            Assert.Contains("already registered", result.Message);
        }

        [Fact]
        public async Task SignupAsync_WithMissingFields_ReturnsBadRequest()
        {
            // Arrange
            var context = CreateInMemoryDbContext();
            var configMock = CreateMockConfiguration();
            var service = new AuthService(context, configMock.Object);

            var request = new SignupRequest
            {
                Email = "",
                Name = "",
                Password = "Password123",
                ConfirmPassword = "Password123"
            };

            // Act
            var result = await service.SignupAsync(request);

            // Assert
            Assert.True(result.IsFailure);
            Assert.Equal(400, result.StatusCode);
        }
    }

    public class AuthServiceVerifyEmailTests
    {
        private PortivioDbContext CreateInMemoryDbContext()
        {
            var options = new DbContextOptionsBuilder<PortivioDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            return new PortivioDbContext(options);
        }

        private Mock<IConfiguration> CreateMockConfiguration()
        {
            var config = new Mock<IConfiguration>();
            config.Setup(c => c["Jwt:Key"])
                .Returns("super-secret-key-that-is-at-least-32-characters-long");
            config.Setup(c => c["Jwt:Issuer"])
                .Returns("Portivio");
            config.Setup(c => c["Jwt:Audience"])
                .Returns("PortivioUsers");
            return config;
        }

        [Fact]
        public async Task VerifyEmailAsync_WithValidEmail_MarksAsVerified()
        {
            // Arrange
            var context = CreateInMemoryDbContext();
            var configMock = CreateMockConfiguration();

            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = "test@example.com",
                Name = "Test User",
                IsVerified = false,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            context.Users.Add(user);
            await context.SaveChangesAsync();

            var service = new AuthService(context, configMock.Object);
            var request = new VerifyEmailRequest
            {
                Email = "test@example.com",
                VerificationToken = "valid-token"
            };

            // Act
            var result = await service.VerifyEmailAsync(request);

            // Assert
            Assert.True(result.IsSuccess);
            Assert.NotNull(result.Data);
            var updatedUser = await context.Users.FirstOrDefaultAsync(u => u.Id == user.Id);
            Assert.True(updatedUser?.IsVerified);
        }

        [Fact]
        public async Task VerifyEmailAsync_WithNonexistentEmail_ReturnsNotFound()
        {
            // Arrange
            var context = CreateInMemoryDbContext();
            var configMock = CreateMockConfiguration();
            var service = new AuthService(context, configMock.Object);

            var request = new VerifyEmailRequest
            {
                Email = "nonexistent@example.com",
                VerificationToken = "token"
            };

            // Act
            var result = await service.VerifyEmailAsync(request);

            // Assert
            Assert.True(result.IsFailure);
            Assert.Equal(404, result.StatusCode);
            Assert.Contains("not found", result.Message);
        }

        [Fact]
        public async Task VerifyEmailAsync_WithAlreadyVerifiedEmail_ReturnsBadRequest()
        {
            // Arrange
            var context = CreateInMemoryDbContext();
            var configMock = CreateMockConfiguration();

            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = "verified@example.com",
                Name = "Verified User",
                IsVerified = true,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            context.Users.Add(user);
            await context.SaveChangesAsync();

            var service = new AuthService(context, configMock.Object);
            var request = new VerifyEmailRequest
            {
                Email = "verified@example.com",
                VerificationToken = "token"
            };

            // Act
            var result = await service.VerifyEmailAsync(request);

            // Assert
            Assert.True(result.IsFailure);
            Assert.Equal(400, result.StatusCode);
            Assert.Contains("already verified", result.Message);
        }
    }

    public class AuthServiceLogoutTests
    {
        private PortivioDbContext CreateInMemoryDbContext()
        {
            var options = new DbContextOptionsBuilder<PortivioDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            return new PortivioDbContext(options);
        }

        private Mock<IConfiguration> CreateMockConfiguration()
        {
            var config = new Mock<IConfiguration>();
            config.Setup(c => c["Jwt:Key"])
                .Returns("super-secret-key-that-is-at-least-32-characters-long");
            config.Setup(c => c["Jwt:Issuer"])
                .Returns("Portivio");
            config.Setup(c => c["Jwt:Audience"])
                .Returns("PortivioUsers");
            return config;
        }

        [Fact]
        public async Task LogoutAsync_RevokesAllUserTokens()
        {
            // Arrange
            var context = CreateInMemoryDbContext();
            var configMock = CreateMockConfiguration();
            var userId = Guid.NewGuid();

            var token1 = new AuthToken
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                AccessTokenHash = "hash1",
                RefreshTokenHash = "hash1",
                AccessTokenExpiry = DateTime.UtcNow.AddHours(1),
                RefreshTokenExpiry = DateTime.UtcNow.AddDays(7),
                DeviceInfo = "Device1",
                IpAddress = "192.168.1.1",
                Revoked = false,
                CreatedAt = DateTime.UtcNow
            };

            var token2 = new AuthToken
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                AccessTokenHash = "hash2",
                RefreshTokenHash = "hash2",
                AccessTokenExpiry = DateTime.UtcNow.AddHours(1),
                RefreshTokenExpiry = DateTime.UtcNow.AddDays(7),
                DeviceInfo = "Device2",
                IpAddress = "192.168.1.2",
                Revoked = false,
                CreatedAt = DateTime.UtcNow
            };

            context.AuthTokens.AddRange(token1, token2);
            await context.SaveChangesAsync();

            var service = new AuthService(context, configMock.Object);

            // Act
            var result = await service.LogoutAsync(userId);

            // Assert
            Assert.True(result.IsSuccess);
            var tokens = await context.AuthTokens.Where(t => t.UserId == userId).ToListAsync();
            Assert.All(tokens, t => Assert.True(t.Revoked));
        }

        [Fact]
        public async Task LogoutAsync_WithNoTokens_ReturnsSuccess()
        {
            // Arrange
            var context = CreateInMemoryDbContext();
            var configMock = CreateMockConfiguration();
            var userId = Guid.NewGuid();

            var service = new AuthService(context, configMock.Object);

            // Act
            var result = await service.LogoutAsync(userId);

            // Assert
            Assert.True(result.IsSuccess);
        }
    }

    public class AuthServiceCleanupTokensTests
    {
        private PortivioDbContext CreateInMemoryDbContext()
        {
            var options = new DbContextOptionsBuilder<PortivioDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            return new PortivioDbContext(options);
        }

        private Mock<IConfiguration> CreateMockConfiguration()
        {
            var config = new Mock<IConfiguration>();
            config.Setup(c => c["Jwt:Key"])
                .Returns("super-secret-key-that-is-at-least-32-characters-long");
            config.Setup(c => c["Jwt:Issuer"])
                .Returns("Portivio");
            config.Setup(c => c["Jwt:Audience"])
                .Returns("PortivioUsers");
            return config;
        }

        [Fact]
        public async Task CleanupExpiredTokensAsync_RemovesExpiredTokens()
        {
            // Arrange
            var context = CreateInMemoryDbContext();
            var configMock = CreateMockConfiguration();

            var expiredToken = new AuthToken
            {
                Id = Guid.NewGuid(),
                UserId = Guid.NewGuid(),
                AccessTokenHash = "expired-hash",
                RefreshTokenHash = "expired-hash",
                AccessTokenExpiry = DateTime.UtcNow.AddHours(-1),
                RefreshTokenExpiry = DateTime.UtcNow.AddDays(-1),
                DeviceInfo = "Device",
                IpAddress = "192.168.1.1",
                Revoked = false,
                CreatedAt = DateTime.UtcNow.AddDays(-8)
            };

            var validToken = new AuthToken
            {
                Id = Guid.NewGuid(),
                UserId = Guid.NewGuid(),
                AccessTokenHash = "valid-hash",
                RefreshTokenHash = "valid-hash",
                AccessTokenExpiry = DateTime.UtcNow.AddHours(1),
                RefreshTokenExpiry = DateTime.UtcNow.AddDays(7),
                DeviceInfo = "Device",
                IpAddress = "192.168.1.2",
                Revoked = false,
                CreatedAt = DateTime.UtcNow
            };

            context.AuthTokens.AddRange(expiredToken, validToken);
            await context.SaveChangesAsync();

            var service = new AuthService(context, configMock.Object);

            // Act
            var result = await service.CleanupExpiredTokensAsync();

            // Assert
            Assert.True(result.IsSuccess);
            var remainingTokens = await context.AuthTokens.ToListAsync();
            Assert.Single(remainingTokens);
            Assert.Equal(validToken.Id, remainingTokens[0].Id);
        }

        [Fact]
        public async Task CleanupExpiredTokensAsync_WithNoExpiredTokens_ReturnsSuccess()
        {
            // Arrange
            var context = CreateInMemoryDbContext();
            var configMock = CreateMockConfiguration();

            var validToken = new AuthToken
            {
                Id = Guid.NewGuid(),
                UserId = Guid.NewGuid(),
                AccessTokenHash = "valid-hash",
                RefreshTokenHash = "valid-hash",
                AccessTokenExpiry = DateTime.UtcNow.AddHours(1),
                RefreshTokenExpiry = DateTime.UtcNow.AddDays(7),
                DeviceInfo = "Device",
                IpAddress = "192.168.1.1",
                Revoked = false,
                CreatedAt = DateTime.UtcNow
            };

            context.AuthTokens.Add(validToken);
            await context.SaveChangesAsync();

            var service = new AuthService(context, configMock.Object);

            // Act
            var result = await service.CleanupExpiredTokensAsync();

            // Assert
            Assert.True(result.IsSuccess);
            var tokens = await context.AuthTokens.ToListAsync();
            Assert.Single(tokens);
        }
    }

    public class AuthServiceRefreshTokenTests
    {
        private PortivioDbContext CreateInMemoryDbContext()
        {
            var options = new DbContextOptionsBuilder<PortivioDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            return new PortivioDbContext(options);
        }

        private Mock<IConfiguration> CreateMockConfiguration()
        {
            var config = new Mock<IConfiguration>();
            config.Setup(c => c["Jwt:Key"])
                .Returns("super-secret-key-that-is-at-least-32-characters-long");
            config.Setup(c => c["Jwt:Issuer"])
                .Returns("Portivio");
            config.Setup(c => c["Jwt:Audience"])
                .Returns("PortivioUsers");
            return config;
        }

        [Fact]
        public async Task RefreshTokenAsync_WithEmptyToken_ReturnsBadRequest()
        {
            // Arrange
            var context = CreateInMemoryDbContext();
            var configMock = CreateMockConfiguration();
            var service = new AuthService(context, configMock.Object);

            // Act
            var result = await service.RefreshTokenAsync("");

            // Assert
            Assert.True(result.IsFailure);
            Assert.Equal(400, result.StatusCode);
            Assert.Contains("required", result.Message);
        }

        [Fact]
        public async Task RefreshTokenAsync_WithInvalidToken_ReturnsUnauthorized()
        {
            // Arrange
            var context = CreateInMemoryDbContext();
            var configMock = CreateMockConfiguration();
            var service = new AuthService(context, configMock.Object);

            // Act
            var result = await service.RefreshTokenAsync("invalid-token");

            // Assert
            Assert.True(result.IsFailure);
            Assert.Equal(401, result.StatusCode);
            Assert.Contains("Invalid", result.Message);
        }

        [Fact]
        public async Task RefreshTokenAsync_WithInactiveUser_ReturnsForbidden()
        {
            var context = CreateInMemoryDbContext();
            var configMock = CreateMockConfiguration();

            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = "inactive@example.com",
                Name = "Inactive User",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Password123"),
                IsVerified = true,
                IsActive = false,
                CreatedAt = DateTime.UtcNow
            };

            var refreshToken = "phone-refresh-token";
            context.Users.Add(user);
            context.AuthTokens.Add(new AuthToken
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                AccessTokenHash = "access-hash",
                RefreshTokenHash = Convert.ToBase64String(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(refreshToken))),
                AccessTokenExpiry = DateTime.UtcNow.AddMinutes(30),
                RefreshTokenExpiry = DateTime.UtcNow.AddDays(7),
                DeviceInfo = "Mozilla/5.0 (Android 14; Mobile)",
                IpAddress = "127.0.0.1",
                Revoked = false,
                CreatedAt = DateTime.UtcNow
            });
            await context.SaveChangesAsync();

            var service = new AuthService(context, configMock.Object);

            var result = await service.RefreshTokenAsync(refreshToken);

            Assert.True(result.IsFailure);
            Assert.Equal(403, result.StatusCode);
            Assert.Equal("Account is inactive", result.Message);
        }
    }
}
