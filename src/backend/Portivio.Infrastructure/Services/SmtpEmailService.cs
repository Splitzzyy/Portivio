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
        var link = BuildVerificationLink(toEmail, verificationToken);
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
        var link = BuildResetPasswordLink(toEmail, resetToken);
        var (subject, body) = EmailTemplates.PasswordResetEmail(toName, link);
        await SendAsync(toEmail, toName, subject, body);
    }

    public async Task SendInvestmentSummaryAsync(InvestmentSummaryEmailModel model, CancellationToken cancellationToken = default)
    {
        var (subject, html, text) = EmailTemplates.InvestmentSummaryEmail(model);
        await SendAsync(model.RegisteredEmail, model.UserName, subject, html, text, cancellationToken);
    }

    private async Task SendAsync(
        string toEmail,
        string toName,
        string subject,
        string htmlBody,
        string? textBody = null,
        CancellationToken cancellationToken = default)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_options.FromName, _options.FromAddress));
        message.To.Add(new MailboxAddress(toName, toEmail));
        message.Subject = subject;
        message.Body = new BodyBuilder { HtmlBody = htmlBody, TextBody = textBody }.ToMessageBody();

        using var client = new SmtpClient();
        try
        {
            var secureOption = _options.EnableSsl ? SecureSocketOptions.StartTls : SecureSocketOptions.None;
            await client.ConnectAsync(_options.Host, _options.Port, secureOption, cancellationToken);

            if (!string.IsNullOrEmpty(_options.Username))
                await client.AuthenticateAsync(_options.Username, _options.Password, cancellationToken);

            await client.SendAsync(message, cancellationToken);
            await client.DisconnectAsync(true, cancellationToken);

            _logger.LogInformation("Email sent. To={To} Subject={Subject}", toEmail, subject);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SMTP send failed. To={To} Subject={Subject} Host={Host}:{Port}",
                toEmail, subject, _options.Host, _options.Port);
            throw;
        }
    }

    private string BuildVerificationLink(string email, string token) =>
        $"{_options.FrontendBaseUrl.TrimEnd('/')}/auth/verify-email?email={Uri.EscapeDataString(email)}&token={Uri.EscapeDataString(token)}";

    private string BuildResetPasswordLink(string email, string token) =>
        $"{_options.FrontendBaseUrl.TrimEnd('/')}/auth/reset-password/{Uri.EscapeDataString(token)}?email={Uri.EscapeDataString(email)}";
}
