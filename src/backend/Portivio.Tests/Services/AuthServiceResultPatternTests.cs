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
    public class AuthServiceResultPatternTests
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
        public async Task LoginAsync_ResultHasProperStatusCode()
        {
            // Arrange
            var context = CreateInMemoryDbContext();
            var configMock = CreateMockConfiguration();

            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = "test@example.com",
                Name = "Test User",
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
                Password = "Password123"
            };

            // Act
            var result = await service.LoginAsync(request);

            // Assert
            Assert.NotNull(result.StatusCode);
            Assert.Equal(200, result.StatusCode);
        }

        [Fact]
        public async Task SignupAsync_SuccessfulResultHasStatusCode201()
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
            Assert.Equal(201, result.StatusCode);
        }

        [Fact]
        public async Task ResultFailure_ContainsErrorMessages()
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
            Assert.NotEmpty(result.Errors);
            Assert.Contains("Invalid credentials", result.Errors);
        }

        [Fact]
        public async Task ResendVerificationEmailAsync_WithValidEmail_ReturnsSuccess()
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

            // Act
            var result = await service.ResendVerificationEmailAsync("test@example.com");

            // Assert
            Assert.True(result.IsSuccess);
            Assert.Equal(200, result.StatusCode);
        }

        [Fact]
        public async Task ResendVerificationEmailAsync_WithAlreadyVerifiedEmail_ReturnsBadRequest()
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

            // Act
            var result = await service.ResendVerificationEmailAsync("verified@example.com");

            // Assert
            Assert.True(result.IsFailure);
            Assert.Equal(400, result.StatusCode);
        }

        [Fact]
        public async Task ForgotPasswordAsync_WithValidEmail_ReturnsSuccess()
        {
            // Arrange
            var context = CreateInMemoryDbContext();
            var configMock = CreateMockConfiguration();

            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = "test@example.com",
                Name = "Test User",
                IsVerified = true,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            context.Users.Add(user);
            await context.SaveChangesAsync();

            var service = new AuthService(context, configMock.Object);

            // Act
            var result = await service.ForgotPasswordAsync(new ForgotPasswordRequest { Email = "test@example.com" });

            // Assert
            Assert.True(result.IsSuccess);
            Assert.Equal(200, result.StatusCode);
        }

        [Fact]
        public async Task ResetPasswordAsync_WithMismatchedPasswords_ReturnsBadRequest()
        {
            // Arrange
            var context = CreateInMemoryDbContext();
            var configMock = CreateMockConfiguration();

            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = "test@example.com",
                Name = "Test User",
                IsVerified = true,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            context.Users.Add(user);
            await context.SaveChangesAsync();

            var service = new AuthService(context, configMock.Object);

            var request = new ResetPasswordRequest
            {
                Email = "test@example.com",
                NewPassword = "NewPassword123",
                ConfirmPassword = "DifferentPassword",
                ResetToken = "token"
            };

            // Act
            var result = await service.ResetPasswordAsync(request);

            // Assert
            Assert.True(result.IsFailure);
            Assert.Equal(400, result.StatusCode);
            Assert.Contains("do not match", result.Message);
        }

        [Fact]
        public async Task GoogleLoginAsync_NotImplemented_ReturnsFunctionalError()
        {
            // Arrange
            var context = CreateInMemoryDbContext();
            var configMock = CreateMockConfiguration();
            var service = new AuthService(context, configMock.Object);

            var request = new GoogleLoginRequest
            {
                Token = "google-token"
            };

            // Act
            var result = await service.GoogleLoginAsync(request);

            // Assert
            Assert.True(result.IsFailure);
            Assert.Equal(501, result.StatusCode); // Not Implemented
        }

        [Fact]
        public async Task AllResults_HaveConsistentStructure()
        {
            // Arrange
            var context = CreateInMemoryDbContext();
            var configMock = CreateMockConfiguration();
            var service = new AuthService(context, configMock.Object);

            // Act
            var loginResult = await service.LoginAsync(new LoginRequest { Email = "", Password = "" });
            var signupResult = await service.SignupAsync(new SignupRequest { Email = "", Name = "", Password = "", ConfirmPassword = "" });
            var logoutResult = await service.LogoutAsync(Guid.NewGuid());

            // Assert - All results should have StatusCode
            Assert.NotNull(loginResult.StatusCode);
            Assert.NotNull(signupResult.StatusCode);
            Assert.NotNull(logoutResult.StatusCode);

            // All failures should have errors
            Assert.NotNull(loginResult.Errors);
            Assert.NotNull(signupResult.Errors);
            Assert.NotEmpty(loginResult.Errors);
            Assert.NotEmpty(signupResult.Errors);
        }

        [Fact]
        public async Task SuccessfulLogin_TokensAreNotEmpty()
        {
            // Arrange
            var context = CreateInMemoryDbContext();
            var configMock = CreateMockConfiguration();

            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = "test@example.com",
                Name = "Test User",
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
                Password = "Password123"
            };

            // Act
            var result = await service.LoginAsync(request);

            // Assert
            Assert.True(result.IsSuccess);
            Assert.NotNull(result.Data);
            Assert.False(string.IsNullOrEmpty(result.Data.AccessToken));
            Assert.False(string.IsNullOrEmpty(result.Data.RefreshToken));
            Assert.NotEqual(result.Data.AccessToken, result.Data.RefreshToken);
        }

        [Fact]
        public async Task TokensArePersisted_AfterSuccessfulLogin()
        {
            // Arrange
            var context = CreateInMemoryDbContext();
            var configMock = CreateMockConfiguration();

            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = "test@example.com",
                Name = "Test User",
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
                Password = "Password123"
            };

            // Act
            var result = await service.LoginAsync(request);

            // Assert
            Assert.True(result.IsSuccess);
            Assert.NotNull(result.Data?.AccessToken);
            Assert.NotNull(result.Data?.RefreshToken);
            var tokens = await context.AuthTokens.Where(t => t.UserId == user.Id).ToListAsync();
            Assert.NotEmpty(tokens);
            Assert.True(tokens.All(t => !t.Revoked));
        }
    }
}
