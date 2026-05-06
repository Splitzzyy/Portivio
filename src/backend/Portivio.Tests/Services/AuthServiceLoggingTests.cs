using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Portivio.Application.DTOs.Auth;
using Portivio.Application.Services;
using Portivio.Domain.Entities;
using Portivio.Infrastructure.Data;
using Portivio.Infrastructure.Services;
using Xunit;

namespace Portivio.Tests.Services
{
    public class AuthServiceLoggingTests
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

        private void VerifyLog(Mock<ILogger<AuthService>> loggerMock, LogLevel level, string messagePart, string propertyName, object propertyValue)
        {
            loggerMock.Verify(
                x => x.Log(
                    level,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => 
                        v != null && 
                        v.ToString()!.Contains(messagePart) && 
                        v.ToString()!.Contains($"{propertyName}={propertyValue}")
                    ),
                    It.IsAny<Exception>(),
                    (Func<It.IsAnyType, Exception?, string>)It.IsAny<object>()),
                Times.AtLeastOnce);
        }

        private void VerifyLog(Mock<ILogger<AuthService>> loggerMock, LogLevel level, string messagePart)
        {
            loggerMock.Verify(
                x => x.Log(
                    level,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v != null && v.ToString()!.Contains(messagePart)),
                    It.IsAny<Exception>(),
                    (Func<It.IsAnyType, Exception?, string>)It.IsAny<object>()),
                Times.AtLeastOnce);
        }

        [Fact]
        public async Task LoginAsync_WithValidCredentials_LogsInformation()
        {
            // Arrange
            var context = CreateInMemoryDbContext();
            var jwtOptions = CreateJwtOptions();
            var loggerMock = new Mock<ILogger<AuthService>>();

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

            var service = new AuthService(context, jwtOptions, Options.Create(new GoogleAuthOptions()), loggerMock.Object, Mock.Of<IEmailJobService>());
            var request = new LoginRequest
            {
                Email = "test@example.com",
                Password = "Password123",
                IpAddress = "127.0.0.1",
                DeviceInfo = "TestDevice"
            };

            // Act
            await service.LoginAsync(request);

            // Assert
            VerifyLog(loggerMock, LogLevel.Information, "Login successful", "Outcome", "Success");
            VerifyLog(loggerMock, LogLevel.Information, "Login successful", "Email", "test@example.com");
            VerifyLog(loggerMock, LogLevel.Information, "Login successful", "UserId", user.Id);
        }

        [Fact]
        public async Task LoginAsync_WithInvalidEmail_LogsWarning()
        {
            // Arrange
            var context = CreateInMemoryDbContext();
            var jwtOptions = CreateJwtOptions();
            var loggerMock = new Mock<ILogger<AuthService>>();
            var service = new AuthService(context, jwtOptions, Options.Create(new GoogleAuthOptions()), loggerMock.Object, Mock.Of<IEmailJobService>());

            var request = new LoginRequest
            {
                Email = "nonexistent@example.com",
                Password = "Password123",
                IpAddress = "127.0.0.1"
            };

            // Act
            await service.LoginAsync(request);

            // Assert
            VerifyLog(loggerMock, LogLevel.Warning, "invalid credentials (user not found)", "Outcome", "Unauthorized");
            VerifyLog(loggerMock, LogLevel.Warning, "invalid credentials (user not found)", "Email", "nonexistent@example.com");
        }

        [Fact]
        public async Task LoginAsync_WithUnverifiedEmail_LogsWarning()
        {
            // Arrange
            var context = CreateInMemoryDbContext();
            var jwtOptions = CreateJwtOptions();
            var loggerMock = new Mock<ILogger<AuthService>>();

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

            var service = new AuthService(context, jwtOptions, Options.Create(new GoogleAuthOptions()), loggerMock.Object, Mock.Of<IEmailJobService>());
            var request = new LoginRequest
            {
                Email = "unverified@example.com",
                Password = "Password123",
                IpAddress = "127.0.0.1"
            };

            // Act
            await service.LoginAsync(request);

            // Assert
            VerifyLog(loggerMock, LogLevel.Warning, "email not verified", "Outcome", "BadRequest");
            VerifyLog(loggerMock, LogLevel.Warning, "email not verified", "UserId", user.Id);
        }

        [Fact]
        public async Task LoginAsync_WithInactiveAccount_LogsWarning()
        {
            // Arrange
            var context = CreateInMemoryDbContext();
            var jwtOptions = CreateJwtOptions();
            var loggerMock = new Mock<ILogger<AuthService>>();

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

            var service = new AuthService(context, jwtOptions, Options.Create(new GoogleAuthOptions()), loggerMock.Object, Mock.Of<IEmailJobService>());
            var request = new LoginRequest
            {
                Email = "inactive@example.com",
                Password = "Password123",
                IpAddress = "127.0.0.1"
            };

            // Act
            await service.LoginAsync(request);

            // Assert
            VerifyLog(loggerMock, LogLevel.Warning, "account is inactive", "Outcome", "Forbidden");
            VerifyLog(loggerMock, LogLevel.Warning, "account is inactive", "UserId", user.Id);
        }

        [Fact]
        public async Task LoginAsync_WithInvalidPassword_LogsWarning()
        {
            // Arrange
            var context = CreateInMemoryDbContext();
            var jwtOptions = CreateJwtOptions();
            var loggerMock = new Mock<ILogger<AuthService>>();

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

            var service = new AuthService(context, jwtOptions, Options.Create(new GoogleAuthOptions()), loggerMock.Object, Mock.Of<IEmailJobService>());
            var request = new LoginRequest
            {
                Email = "test@example.com",
                Password = "WrongPassword",
                IpAddress = "127.0.0.1"
            };

            // Act
            await service.LoginAsync(request);

            // Assert
            VerifyLog(loggerMock, LogLevel.Warning, "invalid credentials (wrong password)", "Outcome", "Unauthorized");
            VerifyLog(loggerMock, LogLevel.Warning, "invalid credentials (wrong password)", "UserId", user.Id);
        }

        [Fact]
        public async Task LoginAsync_WithEmptyCredentials_LogsWarning()
        {
            // Arrange
            var context = CreateInMemoryDbContext();
            var jwtOptions = CreateJwtOptions();
            var loggerMock = new Mock<ILogger<AuthService>>();
            var service = new AuthService(context, jwtOptions, Options.Create(new GoogleAuthOptions()), loggerMock.Object, Mock.Of<IEmailJobService>());

            var request = new LoginRequest
            {
                Email = "",
                Password = "",
                IpAddress = "127.0.0.1"
            };

            // Act
            await service.LoginAsync(request);

            // Assert
            VerifyLog(loggerMock, LogLevel.Warning, "missing credentials", "Outcome", "BadRequest");
        }

        [Fact]
        public async Task LoginAsync_WhenExceptionOccurs_LogsError()
        {
            // Arrange
            var jwtOptions = CreateJwtOptions();
            var loggerMock = new Mock<ILogger<AuthService>>();
            
            // Passing null context to force an exception
            var service = new AuthService(null!, jwtOptions, Options.Create(new GoogleAuthOptions()), loggerMock.Object, Mock.Of<IEmailJobService>());
            
            var request = new LoginRequest
            {
                Email = "test@example.com",
                Password = "Password123",
                IpAddress = "127.0.0.1"
            };

            // Act
            await service.LoginAsync(request);

            // Assert
            VerifyLog(loggerMock, LogLevel.Error, "unexpected exception", "Outcome", "InternalServerError");
        }
    }
}
