using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Portivio.Infrastructure.Services;
using Xunit;

namespace Portivio.Tests.Services;

public class SmtpEmailServiceTests
{
    private readonly Mock<ILogger<SmtpEmailService>> _loggerMock;
    private readonly EmailOptions _options;

    public SmtpEmailServiceTests()
    {
        _loggerMock = new Mock<ILogger<SmtpEmailService>>();
        _options = new EmailOptions
        {
            FrontendBaseUrl = "https://app.portivio.app",
            Host = "localhost",
            Port = 1025,
            FromAddress = "noreply@portivio.app",
            FromName = "Portivio"
        };
    }

    [Fact]
    public void SendEmailVerificationAsync_ShouldUseCorrectLinkShape()
    {
        // Arrange
        var service = CreateService();
        var email = "test@example.com";
        var token = "token123";

        // Act
        var method = typeof(SmtpEmailService).GetMethod("BuildVerificationLink", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Assert.NotNull(method);
        var result = (string?)method.Invoke(service, new object[] { email, token });

        // Assert
        Assert.Equal("https://app.portivio.app/auth/verify-email?email=test%40example.com&token=token123", result);
    }

    [Fact]
    public void SendPasswordResetAsync_ShouldUseCorrectLinkShape()
    {
        // Arrange
        var service = CreateService();
        var email = "test@example.com";
        var token = "token123";

        // Act
        var method = typeof(SmtpEmailService).GetMethod("BuildResetPasswordLink", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Assert.NotNull(method);
        var result = (string?)method.Invoke(service, new object[] { email, token });

        // Assert
        Assert.Equal("https://app.portivio.app/auth/reset-password/token123?email=test%40example.com", result);
    }

    private SmtpEmailService CreateService()
    {
        var optionsMock = new Mock<IOptions<EmailOptions>>();
        optionsMock.Setup(o => o.Value).Returns(_options);
        return new SmtpEmailService(optionsMock.Object, _loggerMock.Object);
    }
}
