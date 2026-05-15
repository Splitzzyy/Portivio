namespace Portivio.Infrastructure.Services;

public interface IEmailService
{
    Task SendEmailVerificationAsync(string toEmail, string toName, string verificationToken);
    Task SendWelcomeEmailAsync(string toEmail, string toName);
    Task SendPasswordResetAsync(string toEmail, string toName, string resetToken);
    Task SendInvestmentSummaryAsync(InvestmentSummaryEmailModel model, CancellationToken cancellationToken = default);
}
