namespace Portivio.Infrastructure.Services;

internal static class EmailTemplates
{
    public static (string Subject, string HtmlBody) VerificationEmail(string name, string verificationLink)
    {
        var subject = "Verify your Portivio email address";
        var body = $"""
            <div style="font-family:Arial,sans-serif;max-width:600px;margin:0 auto">
              <h2>Welcome to Portivio, {name}!</h2>
              <p>Please verify your email address by clicking the link below.</p>
              <p><a href="{verificationLink}" style="background:#2563eb;color:#fff;padding:12px 24px;border-radius:6px;text-decoration:none">Verify Email</a></p>
              <p style="color:#6b7280;font-size:14px">This link expires in 24 hours. If you did not create an account, you can safely ignore this email.</p>
            </div>
            """;
        return (subject, body);
    }

    public static (string Subject, string HtmlBody) WelcomeEmail(string name)
    {
        var subject = "Welcome to Portivio!";
        var body = $"""
            <div style="font-family:Arial,sans-serif;max-width:600px;margin:0 auto">
              <h2>Hello, {name}!</h2>
              <p>Your Portivio account is ready. Start tracking your portfolio today.</p>
              <p style="color:#6b7280;font-size:14px">Questions? Reply to this email and we'll help you get started.</p>
            </div>
            """;
        return (subject, body);
    }

    public static (string Subject, string HtmlBody) PasswordResetEmail(string name, string resetLink)
    {
        var subject = "Reset your Portivio password";
        var body = $"""
            <div style="font-family:Arial,sans-serif;max-width:600px;margin:0 auto">
              <h2>Password Reset Request</h2>
              <p>Hi {name},</p>
              <p>Click the link below to reset your password. This link expires in 1 hour.</p>
              <p><a href="{resetLink}" style="background:#2563eb;color:#fff;padding:12px 24px;border-radius:6px;text-decoration:none">Reset Password</a></p>
              <p style="color:#6b7280;font-size:14px">If you did not request a password reset, you can safely ignore this email.</p>
            </div>
            """;
        return (subject, body);
    }
}
