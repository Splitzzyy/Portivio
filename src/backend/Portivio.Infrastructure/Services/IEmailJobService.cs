namespace Portivio.Infrastructure.Services;

public interface IEmailJobService
{
    void EnqueueVerificationEmail(string toEmail, string toName, string verificationToken);
    void EnqueueWelcomeEmail(string toEmail, string toName);
    void EnqueuePasswordResetEmail(string toEmail, string toName, string resetToken);
}
