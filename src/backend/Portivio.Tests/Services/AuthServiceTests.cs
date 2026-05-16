using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Portivio.Application.DTOs.Auth;
using Portivio.Application.Services;
using Portivio.Domain.Constants;
using Portivio.Domain.Entities;
using Portivio.Domain.Services.Audit;
using Portivio.Infrastructure.Data;
using Portivio.Infrastructure.Services;
using Portivio.Infrastructure.Services.Audit;
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

        private IOptions<AppSettingsOptions> CreateJwtOptions()
        {
            return Options.Create(new AppSettingsOptions
            {
                Key = "super-secret-key-that-is-at-least-32-characters-long",
                Issuer = "Portivio",
                Audience = "PortivioUsers"
            });
        }

        [Fact]
        public async Task LoginAsync_WithValidCredentials_ReturnsSuccessResult()
        {
            // Arrange
            var context = CreateInMemoryDbContext();
            var jwtOptions = CreateJwtOptions();
            var auditServiceMock = new Mock<IAuditService>();

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

            var service = new AuthService(
                context,
                jwtOptions,
                Options.Create(new GoogleAuthOptions()),
                Mock.Of<ILogger<AuthService>>(),
                Mock.Of<IEmailJobService>(),
                auditServiceMock.Object);
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

            auditServiceMock.Verify(a => a.LogAsync(
                    user.Id,
                    AuditActions.Auth_Login_Success,
                    AuditEntities.User,
                    user.Id,
                    null,
                    It.IsAny<object?>()),
                Times.Once);
        }

        [Fact]
        public async Task LoginAsync_WithPhoneClient_ReturnsRefreshToken()
        {
            var context = CreateInMemoryDbContext();
            var jwtOptions = CreateJwtOptions();

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

            var service = new AuthService(context, jwtOptions, Options.Create(new GoogleAuthOptions()), Mock.Of<ILogger<AuthService>>(), Mock.Of<IEmailJobService>(), Mock.Of<IAuditService>());
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
            var jwtOptions = CreateJwtOptions();

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

            var service = new AuthService(context, jwtOptions, Options.Create(new GoogleAuthOptions()), Mock.Of<ILogger<AuthService>>(), Mock.Of<IEmailJobService>(), Mock.Of<IAuditService>());
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
            var jwtOptions = CreateJwtOptions();
            var auditServiceMock = new Mock<IAuditService>();
            var service = new AuthService(context, jwtOptions, Options.Create(new GoogleAuthOptions()), Mock.Of<ILogger<AuthService>>(), Mock.Of<IEmailJobService>(), auditServiceMock.Object);

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
            var jwtOptions = CreateJwtOptions();
            var auditServiceMock = new Mock<IAuditService>();

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

            var service = new AuthService(context, jwtOptions, Options.Create(new GoogleAuthOptions()), Mock.Of<ILogger<AuthService>>(), Mock.Of<IEmailJobService>(), auditServiceMock.Object);
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

            auditServiceMock.Verify(a => a.LogAsync(
                    user.Id,
                    AuditActions.Auth_Login_Failure,
                    AuditEntities.User,
                    user.Id,
                    null,
                    It.Is<Dictionary<string, object?>>(d => (string)d["Reason"]! == "EmailNotVerified")),
                Times.Once);
        }

        [Fact]
        public async Task LoginAsync_WithInactiveAccount_ReturnsForbidden()
        {
            // Arrange
            var context = CreateInMemoryDbContext();
            var jwtOptions = CreateJwtOptions();

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

            var service = new AuthService(context, jwtOptions, Options.Create(new GoogleAuthOptions()), Mock.Of<ILogger<AuthService>>(), Mock.Of<IEmailJobService>(), Mock.Of<IAuditService>());
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
            var jwtOptions = CreateJwtOptions();
            var auditServiceMock = new Mock<IAuditService>();
            var service = new AuthService(context, jwtOptions, Options.Create(new GoogleAuthOptions()), Mock.Of<ILogger<AuthService>>(), Mock.Of<IEmailJobService>(), auditServiceMock.Object);

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

            auditServiceMock.Verify(a => a.LogAsync(
                    null,
                    AuditActions.Auth_Login_Failure,
                    AuditEntities.User,
                    Guid.Empty,
                    null,
                    It.Is<Dictionary<string, object?>>(d =>
                        (string)d["Reason"]! == "MissingCredentials"
                        && (string)d["Email"]! == string.Empty)),
                Times.Once);
        }

        [Fact]
        public async Task LoginAsync_UpdatesLastLoginTime()
        {
            // Arrange
            var context = CreateInMemoryDbContext();
            var jwtOptions = CreateJwtOptions();

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

            var service = new AuthService(context, jwtOptions, Options.Create(new GoogleAuthOptions()), Mock.Of<ILogger<AuthService>>(), Mock.Of<IEmailJobService>(), Mock.Of<IAuditService>());
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

        [Fact]
        public async Task LoginAsync_WithInvalidPassword_AuditsFailureWithReason()
        {
            var context = CreateInMemoryDbContext();
            var jwtOptions = CreateJwtOptions();
            var auditServiceMock = new Mock<IAuditService>();

            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = "test@example.com",
                Name = "Test User",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("CorrectPassword"),
                IsVerified = true,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            context.Users.Add(user);
            await context.SaveChangesAsync();

            var service = new AuthService(context, jwtOptions, Options.Create(new GoogleAuthOptions()), Mock.Of<ILogger<AuthService>>(), Mock.Of<IEmailJobService>(), auditServiceMock.Object);
            var result = await service.LoginAsync(new LoginRequest
            {
                Email = "test@example.com",
                Password = "WrongPassword"
            });

            Assert.True(result.IsFailure);
            Assert.Equal(401, result.StatusCode);

            auditServiceMock.Verify(a => a.LogAsync(
                    user.Id,
                    AuditActions.Auth_Login_Failure,
                    AuditEntities.User,
                    user.Id,
                    null,
                    It.Is<Dictionary<string, object?>>(d => (string)d["Reason"]! == "WrongPassword")),
                Times.Once);
        }

        [Fact]
        public async Task LoginAsync_WhenTokenGenerationFails_DoesNotPersistPendingAuthStateThroughAudit()
        {
            var context = CreateInMemoryDbContext();
            var jwtOptions = Options.Create(new AppSettingsOptions
            {
                Key = "short",
                Issuer = "Portivio",
                Audience = "PortivioUsers"
            });
            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = "token-fail@example.com",
                Name = "Token Fail User",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Password123"),
                IsVerified = true,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                LastLoginAt = null
            };

            context.Users.Add(user);
            await context.SaveChangesAsync();

            var auditService = new AuditService(context, new HttpContextAccessor());
            var service = new AuthService(
                context,
                jwtOptions,
                Options.Create(new GoogleAuthOptions()),
                Mock.Of<ILogger<AuthService>>(),
                Mock.Of<IEmailJobService>(),
                auditService);

            var result = await service.LoginAsync(new LoginRequest
            {
                Email = "token-fail@example.com",
                Password = "Password123",
                IssueRefreshToken = true
            });

            Assert.True(result.IsFailure);

            var updatedUser = await context.Users.SingleAsync(u => u.Id == user.Id);
            Assert.Null(updatedUser.LastLoginAt);
            Assert.Empty(await context.AuthTokens.Where(t => t.UserId == user.Id).ToListAsync());

            var auditLog = await context.AuditLogs.SingleAsync();
            Assert.Equal(AuditActions.Auth_Login_Failure, auditLog.Action);
            Assert.Contains("TokenGenerationFailure", auditLog.NewValues);
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

        private IOptions<AppSettingsOptions> CreateJwtOptions()
        {
            return Options.Create(new AppSettingsOptions
            {
                Key = "super-secret-key-that-is-at-least-32-characters-long",
                Issuer = "Portivio",
                Audience = "PortivioUsers"
            });
        }

        [Fact]
        public async Task SignupAsync_WithValidData_CreatesUserSuccessfully()
        {
            // Arrange
            var context = CreateInMemoryDbContext();
            var jwtOptions = CreateJwtOptions();
            var auditServiceMock = new Mock<IAuditService>();
            var service = new AuthService(context, jwtOptions, Options.Create(new GoogleAuthOptions()), Mock.Of<ILogger<AuthService>>(), Mock.Of<IEmailJobService>(), auditServiceMock.Object);

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

            auditServiceMock.Verify(a => a.LogAsync(
                    createdUser.Id,
                    AuditActions.Auth_Signup_Success,
                    AuditEntities.User,
                    createdUser.Id,
                    null,
                    It.IsAny<object?>()),
                Times.Once);
        }

        [Fact]
        public async Task SignupAsync_WithPasswordMismatch_ReturnsBadRequest()
        {
            // Arrange
            var context = CreateInMemoryDbContext();
            var jwtOptions = CreateJwtOptions();
            var service = new AuthService(context, jwtOptions, Options.Create(new GoogleAuthOptions()), Mock.Of<ILogger<AuthService>>(), Mock.Of<IEmailJobService>(), Mock.Of<IAuditService>());

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
            var jwtOptions = CreateJwtOptions();

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

            var service = new AuthService(context, jwtOptions, Options.Create(new GoogleAuthOptions()), Mock.Of<ILogger<AuthService>>(), Mock.Of<IEmailJobService>(), Mock.Of<IAuditService>());
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
            var jwtOptions = CreateJwtOptions();
            var service = new AuthService(context, jwtOptions, Options.Create(new GoogleAuthOptions()), Mock.Of<ILogger<AuthService>>(), Mock.Of<IEmailJobService>(), Mock.Of<IAuditService>());

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

    public class AuthServiceGoogleLoginTests
    {
        private PortivioDbContext CreateInMemoryDbContext()
        {
            var options = new DbContextOptionsBuilder<PortivioDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            return new PortivioDbContext(options);
        }

        private IOptions<AppSettingsOptions> CreateJwtOptions()
        {
            return Options.Create(new AppSettingsOptions
            {
                Key = "super-secret-key-that-is-at-least-32-characters-long",
                Issuer = "Portivio",
                Audience = "PortivioUsers"
            });
        }

        [Fact]
        public async Task GoogleLoginAsync_WithMissingToken_AuditsFailureWithReason()
        {
            var context = CreateInMemoryDbContext();
            var jwtOptions = CreateJwtOptions();
            var auditServiceMock = new Mock<IAuditService>();
            var service = new AuthService(
                context,
                jwtOptions,
                Options.Create(new GoogleAuthOptions { ClientId = "configured-client-id" }),
                Mock.Of<ILogger<AuthService>>(),
                Mock.Of<IEmailJobService>(),
                auditServiceMock.Object);

            var result = await service.GoogleLoginAsync(new GoogleLoginRequest
            {
                Token = "",
                IpAddress = "127.0.0.1",
                DeviceInfo = "Chrome"
            });

            Assert.True(result.IsFailure);
            Assert.Equal(400, result.StatusCode);

            auditServiceMock.Verify(a => a.LogAsync(
                    null,
                    AuditActions.Auth_GoogleLogin_Failure,
                    AuditEntities.User,
                    Guid.Empty,
                    null,
                    It.Is<Dictionary<string, object?>>(d =>
                        (string)d["Reason"]! == "MissingToken"
                        && (string)d["IpAddress"]! == "127.0.0.1"
                        && (string)d["DeviceInfo"]! == "Chrome")),
                Times.Once);
        }

        [Fact]
        public async Task GoogleLoginAsync_WithoutClientId_AuditsFailureWithReason()
        {
            var context = CreateInMemoryDbContext();
            var jwtOptions = CreateJwtOptions();
            var auditServiceMock = new Mock<IAuditService>();
            var service = new AuthService(
                context,
                jwtOptions,
                Options.Create(new GoogleAuthOptions()),
                Mock.Of<ILogger<AuthService>>(),
                Mock.Of<IEmailJobService>(),
                auditServiceMock.Object);

            var result = await service.GoogleLoginAsync(new GoogleLoginRequest
            {
                Token = "google-token",
                IpAddress = "127.0.0.1",
                DeviceInfo = "Chrome"
            });

            Assert.True(result.IsFailure);
            Assert.Equal(500, result.StatusCode);

            auditServiceMock.Verify(a => a.LogAsync(
                    null,
                    AuditActions.Auth_GoogleLogin_Failure,
                    AuditEntities.User,
                    Guid.Empty,
                    null,
                    It.Is<Dictionary<string, object?>>(d =>
                        (string)d["Reason"]! == "ClientIdNotConfigured"
                        && (string)d["IpAddress"]! == "127.0.0.1"
                        && (string)d["DeviceInfo"]! == "Chrome")),
                Times.Once);
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

        private IOptions<AppSettingsOptions> CreateJwtOptions()
        {
            return Options.Create(new AppSettingsOptions
            {
                Key = "super-secret-key-that-is-at-least-32-characters-long",
                Issuer = "Portivio",
                Audience = "PortivioUsers"
            });
        }

        [Fact]
        public async Task VerifyEmailAsync_WithValidEmail_MarksAsVerified()
        {
            // Arrange
            var context = CreateInMemoryDbContext();
            var jwtOptions = CreateJwtOptions();

            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = "test@example.com",
                Name = "Test User",
                IsVerified = false,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                EmailVerificationToken = "valid-token",
                EmailVerificationTokenExpiry = DateTime.UtcNow.AddHours(24)
            };

            context.Users.Add(user);
            await context.SaveChangesAsync();

            var service = new AuthService(context, jwtOptions, Options.Create(new GoogleAuthOptions()), Mock.Of<ILogger<AuthService>>(), Mock.Of<IEmailJobService>(), Mock.Of<IAuditService>());
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
            var jwtOptions = CreateJwtOptions();
            var service = new AuthService(context, jwtOptions, Options.Create(new GoogleAuthOptions()), Mock.Of<ILogger<AuthService>>(), Mock.Of<IEmailJobService>(), Mock.Of<IAuditService>());

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
            var jwtOptions = CreateJwtOptions();

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

            var service = new AuthService(context, jwtOptions, Options.Create(new GoogleAuthOptions()), Mock.Of<ILogger<AuthService>>(), Mock.Of<IEmailJobService>(), Mock.Of<IAuditService>());
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

        private IOptions<AppSettingsOptions> CreateJwtOptions()
        {
            return Options.Create(new AppSettingsOptions
            {
                Key = "super-secret-key-that-is-at-least-32-characters-long",
                Issuer = "Portivio",
                Audience = "PortivioUsers"
            });
        }

        [Fact]
        public async Task LogoutAsync_RevokesAllUserTokens()
        {
            // Arrange
            var context = CreateInMemoryDbContext();
            var jwtOptions = CreateJwtOptions();
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

            var auditServiceMock = new Mock<IAuditService>();
            var service = new AuthService(context, jwtOptions, Options.Create(new GoogleAuthOptions()), Mock.Of<ILogger<AuthService>>(), Mock.Of<IEmailJobService>(), auditServiceMock.Object);

            // Act
            var result = await service.LogoutAsync(userId);

            // Assert
            Assert.True(result.IsSuccess);
            var tokens = await context.AuthTokens.Where(t => t.UserId == userId).ToListAsync();
            Assert.All(tokens, t => Assert.True(t.Revoked));

            auditServiceMock.Verify(a => a.LogAsync(
                    userId,
                    AuditActions.Auth_Logout,
                    AuditEntities.User,
                    userId,
                    null,
                    It.IsAny<object?>()),
                Times.Once);
        }

        [Fact]
        public async Task LogoutAsync_WithNoTokens_ReturnsSuccess()
        {
            // Arrange
            var context = CreateInMemoryDbContext();
            var jwtOptions = CreateJwtOptions();
            var userId = Guid.NewGuid();

            var auditServiceMock = new Mock<IAuditService>();
            var service = new AuthService(context, jwtOptions, Options.Create(new GoogleAuthOptions()), Mock.Of<ILogger<AuthService>>(), Mock.Of<IEmailJobService>(), auditServiceMock.Object);

            // Act
            var result = await service.LogoutAsync(userId);

            // Assert
            Assert.True(result.IsSuccess);

            auditServiceMock.Verify(a => a.LogAsync(
                    userId,
                    AuditActions.Auth_Logout,
                    AuditEntities.User,
                    userId,
                    null,
                    It.IsAny<object?>()),
                Times.Once);
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

        private IOptions<AppSettingsOptions> CreateJwtOptions()
        {
            return Options.Create(new AppSettingsOptions
            {
                Key = "super-secret-key-that-is-at-least-32-characters-long",
                Issuer = "Portivio",
                Audience = "PortivioUsers"
            });
        }

        [Fact]
        public async Task CleanupExpiredTokensAsync_RemovesExpiredTokens()
        {
            // Arrange
            var context = CreateInMemoryDbContext();
            var jwtOptions = CreateJwtOptions();

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

            var service = new AuthService(context, jwtOptions, Options.Create(new GoogleAuthOptions()), Mock.Of<ILogger<AuthService>>(), Mock.Of<IEmailJobService>(), Mock.Of<IAuditService>());

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
            var jwtOptions = CreateJwtOptions();

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

            var service = new AuthService(context, jwtOptions, Options.Create(new GoogleAuthOptions()), Mock.Of<ILogger<AuthService>>(), Mock.Of<IEmailJobService>(), Mock.Of<IAuditService>());

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

        private IOptions<AppSettingsOptions> CreateJwtOptions()
        {
            return Options.Create(new AppSettingsOptions
            {
                Key = "super-secret-key-that-is-at-least-32-characters-long",
                Issuer = "Portivio",
                Audience = "PortivioUsers"
            });
        }

        [Fact]
        public async Task RefreshTokenAsync_WithEmptyToken_ReturnsBadRequest()
        {
            // Arrange
            var context = CreateInMemoryDbContext();
            var jwtOptions = CreateJwtOptions();
            var service = new AuthService(context, jwtOptions, Options.Create(new GoogleAuthOptions()), Mock.Of<ILogger<AuthService>>(), Mock.Of<IEmailJobService>(), Mock.Of<IAuditService>());

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
            var jwtOptions = CreateJwtOptions();
            var service = new AuthService(context, jwtOptions, Options.Create(new GoogleAuthOptions()), Mock.Of<ILogger<AuthService>>(), Mock.Of<IEmailJobService>(), Mock.Of<IAuditService>());

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
            var jwtOptions = CreateJwtOptions();

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

            var service = new AuthService(context, jwtOptions, Options.Create(new GoogleAuthOptions()), Mock.Of<ILogger<AuthService>>(), Mock.Of<IEmailJobService>(), Mock.Of<IAuditService>());

            var result = await service.RefreshTokenAsync(refreshToken);

            Assert.True(result.IsFailure);
            Assert.Equal(403, result.StatusCode);
            Assert.Equal("Account is inactive", result.Message);
        }
    }

    public class AuthServiceProfileTests
    {
        private PortivioDbContext CreateInMemoryDbContext()
        {
            var options = new DbContextOptionsBuilder<PortivioDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
            return new PortivioDbContext(options);
        }

        private IOptions<AppSettingsOptions> CreateJwtOptions() => Options.Create(new AppSettingsOptions { Key = "super-secret-key-that-is-at-least-32-characters-long", Issuer = "Portivio", Audience = "PortivioUsers" });

        [Fact]
        public async Task UpdateProfileAsync_WithValidData_UpdatesUser()
        {
            var context = CreateInMemoryDbContext();
            var user = new User { Id = Guid.NewGuid(), Email = "test@ex.com", Name = "Old Name", IsActive = true, IsVerified = true };
            context.Users.Add(user);
            await context.SaveChangesAsync();

            var service = new AuthService(context, CreateJwtOptions(), Options.Create(new GoogleAuthOptions()), Mock.Of<ILogger<AuthService>>(), Mock.Of<IEmailJobService>(), Mock.Of<IAuditService>());
            var result = await service.UpdateProfileAsync(user.Id, new UpdateUserProfileRequest { Name = "New Name" });

            Assert.True(result.IsSuccess);
            Assert.Equal("New Name", user.Name);
            Assert.Equal("New Name", result.Data!.User!.Name);
        }

        [Fact]
        public async Task ChangePasswordAsync_WithValidData_UpdatesPassword()
        {
            var context = CreateInMemoryDbContext();
            var oldHash = BCrypt.Net.BCrypt.HashPassword("OldPass123");
            var user = new User { Id = Guid.NewGuid(), Email = "test@ex.com", Name = "Name", PasswordHash = oldHash, IsActive = true, IsVerified = true };
            context.Users.Add(user);
            await context.SaveChangesAsync();

            var auditServiceMock = new Mock<IAuditService>();
            var service = new AuthService(context, CreateJwtOptions(), Options.Create(new GoogleAuthOptions()), Mock.Of<ILogger<AuthService>>(), Mock.Of<IEmailJobService>(), auditServiceMock.Object);
            var req = new ChangePasswordRequest { NewPassword = "NewPassword123", ConfirmPassword = "NewPassword123" };
            var result = await service.ChangePasswordAsync(user.Id, req);

            Assert.True(result.IsSuccess);
            Assert.NotEqual(oldHash, user.PasswordHash);
            Assert.True(BCrypt.Net.BCrypt.Verify("NewPassword123", user.PasswordHash));

            auditServiceMock.Verify(a => a.LogAsync(
                    user.Id,
                    AuditActions.Auth_ChangePassword_Success,
                    AuditEntities.User,
                    user.Id,
                    null,
                    It.Is<object?>(v => AuditValuePredicates.PasswordChangedNewValues(v, req.NewPassword))),
                Times.Once);
        }

        [Fact]
        public async Task ChangePasswordAsync_WithMismatchingPasswords_ReturnsBadRequest()
        {
            var context = CreateInMemoryDbContext();
            var user = new User { Id = Guid.NewGuid(), Email = "test@ex.com", Name = "Name", IsActive = true, IsVerified = true };
            context.Users.Add(user);
            await context.SaveChangesAsync();

            var service = new AuthService(context, CreateJwtOptions(), Options.Create(new GoogleAuthOptions()), Mock.Of<ILogger<AuthService>>(), Mock.Of<IEmailJobService>(), Mock.Of<IAuditService>());
            var req = new ChangePasswordRequest { NewPassword = "NewPassword123", ConfirmPassword = "DifferentPassword" };
            var result = await service.ChangePasswordAsync(user.Id, req);

            Assert.False(result.IsSuccess);
            Assert.Equal(400, result.StatusCode);
            Assert.Equal("Passwords do not match", result.Message);
        }
    }

    public class AuthServiceAccountModificationAuditTests
    {
        private PortivioDbContext CreateInMemoryDbContext()
        {
            var options = new DbContextOptionsBuilder<PortivioDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
            return new PortivioDbContext(options);
        }

        private IOptions<AppSettingsOptions> CreateJwtOptions()
        {
            return Options.Create(new AppSettingsOptions
            {
                Key = "super-secret-key-that-is-at-least-32-characters-long",
                Issuer = "Portivio",
                Audience = "PortivioUsers"
            });
        }

        [Fact]
        public async Task VerifyEmailAsync_OnSuccess_LogsAuditWithoutToken()
        {
            var context = CreateInMemoryDbContext();
            var auditServiceMock = new Mock<IAuditService>();

            var token = "verify-token";
            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = "verify@example.com",
                Name = "Verify User",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Password123"),
                IsVerified = false,
                IsActive = true,
                EmailVerificationToken = token,
                EmailVerificationTokenExpiry = DateTime.UtcNow.AddHours(2),
                CreatedAt = DateTime.UtcNow
            };

            context.Users.Add(user);
            await context.SaveChangesAsync();

            var service = new AuthService(
                context,
                CreateJwtOptions(),
                Options.Create(new GoogleAuthOptions()),
                Mock.Of<ILogger<AuthService>>(),
                Mock.Of<IEmailJobService>(),
                auditServiceMock.Object);

            var result = await service.VerifyEmailAsync(new VerifyEmailRequest { Email = user.Email, VerificationToken = token });

            Assert.True(result.IsSuccess);
            Assert.True(user.IsVerified);
            Assert.Null(user.EmailVerificationToken);

            auditServiceMock.Verify(a => a.LogAsync(
                    user.Id,
                    AuditActions.Auth_VerifyEmail_Success,
                    AuditEntities.User,
                    user.Id,
                    null,
                    It.Is<object?>(v => AuditValuePredicates.RequiresKeyAndNoForbiddenValues(v, "Email", token))),
                Times.Once);
        }

        [Fact]
        public async Task ForgotPasswordAsync_OnSuccess_LogsAuditWithoutResetToken()
        {
            var context = CreateInMemoryDbContext();
            var auditServiceMock = new Mock<IAuditService>();

            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = "forgot@example.com",
                Name = "Forgot User",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Password123"),
                IsVerified = true,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            context.Users.Add(user);
            await context.SaveChangesAsync();

            var service = new AuthService(
                context,
                CreateJwtOptions(),
                Options.Create(new GoogleAuthOptions()),
                Mock.Of<ILogger<AuthService>>(),
                Mock.Of<IEmailJobService>(),
                auditServiceMock.Object);

            var result = await service.ForgotPasswordAsync(new ForgotPasswordRequest { Email = user.Email });

            Assert.True(result.IsSuccess);
            Assert.False(string.IsNullOrWhiteSpace(user.PasswordResetToken));

            var savedResetToken = user.PasswordResetToken!;

            auditServiceMock.Verify(a => a.LogAsync(
                    user.Id,
                    AuditActions.Auth_ForgotPassword_Requested,
                    AuditEntities.User,
                    user.Id,
                    null,
                    It.Is<object?>(v => AuditValuePredicates.RequiresKeyAndNoForbiddenValues(v, "Email", savedResetToken))),
                Times.Once);
        }

        [Fact]
        public async Task ResetPasswordAsync_OnSuccess_LogsAuditWithoutPasswordOrToken()
        {
            var context = CreateInMemoryDbContext();
            var auditServiceMock = new Mock<IAuditService>();

            var resetToken = "reset-token";
            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = "reset@example.com",
                Name = "Reset User",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("OldPassword123"),
                IsVerified = true,
                IsActive = false,
                PasswordResetToken = resetToken,
                PasswordResetTokenExpiry = DateTime.UtcNow.AddHours(1),
                CreatedAt = DateTime.UtcNow
            };

            context.Users.Add(user);
            await context.SaveChangesAsync();

            var service = new AuthService(
                context,
                CreateJwtOptions(),
                Options.Create(new GoogleAuthOptions()),
                Mock.Of<ILogger<AuthService>>(),
                Mock.Of<IEmailJobService>(),
                auditServiceMock.Object);

            var newPassword = "NewPassword123";
            var result = await service.ResetPasswordAsync(new ResetPasswordRequest
            {
                Email = user.Email,
                ResetToken = resetToken,
                NewPassword = newPassword,
                ConfirmPassword = newPassword
            });

            Assert.True(result.IsSuccess);
            Assert.True(user.IsActive);
            Assert.Null(user.PasswordResetToken);
            Assert.True(BCrypt.Net.BCrypt.Verify(newPassword, user.PasswordHash));

            auditServiceMock.Verify(a => a.LogAsync(
                    user.Id,
                    AuditActions.Auth_ResetPassword_Success,
                    AuditEntities.User,
                    user.Id,
                    null,
                    It.Is<object?>(v => AuditValuePredicates.RequiresKeyAndNoForbiddenValues(v, "Email", resetToken, newPassword))),
                Times.Once);
        }
    }

    internal static class AuditValuePredicates
    {
        public static bool PasswordChangedNewValues(object? value, string forbiddenPassword)
        {
            if (value is not IDictionary<string, object?> dict)
                return false;

            if (!dict.TryGetValue("PasswordChanged", out var passwordChanged)
                || passwordChanged is not bool passwordChangedBool
                || !passwordChangedBool)
                return false;

            return dict.Values.All(val => val?.ToString() != forbiddenPassword);
        }

        public static bool RequiresKeyAndNoForbiddenValues(object? value, string requiredKey, params string[] forbiddenValues)
        {
            if (value is not IDictionary<string, object?> dict)
                return false;

            if (!dict.ContainsKey(requiredKey))
                return false;

            foreach (var forbiddenValue in forbiddenValues)
            {
                if (dict.Values.Any(val => val?.ToString() == forbiddenValue))
                    return false;
            }

            return true;
        }
    }
}
