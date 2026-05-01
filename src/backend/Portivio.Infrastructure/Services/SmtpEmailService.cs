using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Portivio.Infrastructure.Services;

public class SmtpEmailService : IEmailService
{
    private readonly EmailOptions _options;
    private readonly ILogger<SmtpEmailService> _logger;

    public SmtpEmailService(IOptions<EmailOptions> options, ILogger<SmtpEmailService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task SendEmailVerificationAsync(string toEmail, string toName, string verificationToken)
    {
        var link = BuildLink("verify-email", toEmail, verificationToken);
        var (subject, body) = EmailTemplates.VerificationEmail(toName, link);
        await SendAsync(toEmail, toName, subject, body);
    }

    public async Task SendWelcomeEmailAsync(string toEmail, string toName)
    {
        var (subject, body) = EmailTemplates.WelcomeEmail(toName);
        await SendAsync(toEmail, toName, subject, body);
    }

    public async Task SendPasswordResetAsync(string toEmail, string toName, string resetToken)
    {
        var link = BuildLink("reset-password", toEmail, resetToken);
        var (subject, body) = EmailTemplates.PasswordResetEmail(toName, link);
        await SendAsync(toEmail, toName, subject, body);
    }

    private async Task SendAsync(string toEmail, string toName, string subject, string htmlBody)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_options.FromName, _options.FromAddress));
        message.To.Add(new MailboxAddress(toName, toEmail));
        message.Subject = subject;
        message.Body = new BodyBuilder { HtmlBody = htmlBody }.ToMessageBody();

        using var client = new SmtpClient();
        try
        {
            var secureOption = _options.EnableSsl ? SecureSocketOptions.StartTls : SecureSocketOptions.None;
            await client.ConnectAsync(_options.Host, _options.Port, secureOption);

            if (!string.IsNullOrEmpty(_options.Username))
                await client.AuthenticateAsync(_options.Username, _options.Password);

            await client.SendAsync(message);
            await client.DisconnectAsync(true);

            _logger.LogInformation("Email sent. To={To} Subject={Subject}", toEmail, subject);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SMTP send failed. To={To} Subject={Subject} Host={Host}:{Port}",
                toEmail, subject, _options.Host, _options.Port);
            throw;
        }
    }

    private string BuildLink(string path, string email, string token) =>
        $"{_options.FrontendBaseUrl.TrimEnd('/')}/{path}?email={Uri.EscapeDataString(email)}&token={Uri.EscapeDataString(token)}";
}
