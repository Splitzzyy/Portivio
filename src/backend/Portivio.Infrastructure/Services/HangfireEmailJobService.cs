using Hangfire;

namespace Portivio.Infrastructure.Services;

public class HangfireEmailJobService : IEmailJobService
{
    private readonly IBackgroundJobClient _backgroundJobClient;

    public HangfireEmailJobService(IBackgroundJobClient backgroundJobClient)
    {
        _backgroundJobClient = backgroundJobClient;
    }

    public void EnqueueVerificationEmail(string toEmail, string toName, string verificationToken) =>
        _backgroundJobClient.Enqueue<IEmailService>(
            svc => svc.SendEmailVerificationAsync(toEmail, toName, verificationToken));

    public void EnqueueWelcomeEmail(string toEmail, string toName) =>
        _backgroundJobClient.Enqueue<IEmailService>(
            svc => svc.SendWelcomeEmailAsync(toEmail, toName));

    public void EnqueuePasswordResetEmail(string toEmail, string toName, string resetToken) =>
        _backgroundJobClient.Enqueue<IEmailService>(
            svc => svc.SendPasswordResetAsync(toEmail, toName, resetToken));
}
