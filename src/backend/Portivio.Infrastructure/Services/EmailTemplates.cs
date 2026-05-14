using System.Globalization;

namespace Portivio.Infrastructure.Services;

public static class EmailTemplates
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

    public static (string Subject, string HtmlBody, string TextBody) InvestmentSummaryEmail(
        InvestmentSummaryEmailModel model,
        IFormatProvider? formatProvider = null)
    {
        var provider = formatProvider ?? CultureInfo.GetCultureInfo("en-IN");
        var subject = "Your Portivio Investment Summary";

        var emptyCopyHtml = model.IsEmptyAccount
            ? "<p><strong>No investment data yet.</strong> Add your first investment to start tracking your portfolio.</p>"
            : string.Empty;

        var emptyCopyText = model.IsEmptyAccount
            ? "No investment data yet. Add your first investment to start tracking your portfolio.\n"
            : string.Empty;

        var totalInvestment = model.TotalInvestment.ToString("C", provider);
        var marketValue = model.MarketValue.ToString("C", provider);
        var unrealizedPnl = model.UnrealizedPnL.ToString("C", provider);
        var returnPct = $"{model.ReturnPercentage:0.##}%";

        var html = $"""
            <div style="font-family:Arial,sans-serif;max-width:720px;margin:0 auto">
              <h2>Investment Summary</h2>
              <p>Hi {model.UserName},</p>
              <p>Registered email: <strong>{model.RegisteredEmail}</strong></p>
              <p style="color:#6b7280;font-size:14px">Generated: {model.GeneratedAtUtc:u}</p>

              {emptyCopyHtml}

              <table style="border-collapse:collapse;width:100%;margin-top:12px">
                <tr>
                  <td style="padding:8px;border-bottom:1px solid #e5e7eb">Profiles</td>
                  <td style="padding:8px;border-bottom:1px solid #e5e7eb;text-align:right">{model.ProfileCount}</td>
                </tr>
                <tr>
                  <td style="padding:8px;border-bottom:1px solid #e5e7eb">Holdings</td>
                  <td style="padding:8px;border-bottom:1px solid #e5e7eb;text-align:right">{model.HoldingCount}</td>
                </tr>
                <tr>
                  <td style="padding:8px;border-bottom:1px solid #e5e7eb">Transactions</td>
                  <td style="padding:8px;border-bottom:1px solid #e5e7eb;text-align:right">{model.TransactionCount}</td>
                </tr>
                <tr>
                  <td style="padding:8px;border-bottom:1px solid #e5e7eb">Active SIPs</td>
                  <td style="padding:8px;border-bottom:1px solid #e5e7eb;text-align:right">{model.ActiveSipCount}</td>
                </tr>
                <tr>
                  <td style="padding:8px;border-bottom:1px solid #e5e7eb">Total investment</td>
                  <td style="padding:8px;border-bottom:1px solid #e5e7eb;text-align:right">{totalInvestment}</td>
                </tr>
                <tr>
                  <td style="padding:8px;border-bottom:1px solid #e5e7eb">Market value</td>
                  <td style="padding:8px;border-bottom:1px solid #e5e7eb;text-align:right">{marketValue}</td>
                </tr>
                <tr>
                  <td style="padding:8px;border-bottom:1px solid #e5e7eb">Unrealized P/L</td>
                  <td style="padding:8px;border-bottom:1px solid #e5e7eb;text-align:right">{unrealizedPnl} ({returnPct})</td>
                </tr>
              </table>

              <p style="margin-top:18px">
                <a href="{model.DashboardLink}" style="background:#2563eb;color:#fff;padding:12px 20px;border-radius:6px;text-decoration:none">Open Dashboard</a>
              </p>
              <p style="color:#6b7280;font-size:14px">
                Manage email summary preferences: <a href="{model.ManagePreferencesLink}">{model.ManagePreferencesLink}</a>
              </p>
            </div>
            """;

        var text = $"""
            Investment Summary
            Hi {model.UserName},
            Registered email: {model.RegisteredEmail}
            Generated: {model.GeneratedAtUtc:u}

            {emptyCopyText}Profiles: {model.ProfileCount}
            Holdings: {model.HoldingCount}
            Transactions: {model.TransactionCount}
            Active SIPs: {model.ActiveSipCount}
            Total investment: {totalInvestment}
            Market value: {marketValue}
            Unrealized P/L: {unrealizedPnl} ({returnPct})

            Dashboard: {model.DashboardLink}
            Preferences: {model.ManagePreferencesLink}
            """;

        return (subject, html, text);
    }
}
