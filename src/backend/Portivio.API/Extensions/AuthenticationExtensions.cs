using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Portivio.Application.Services;
using Serilog;
using System.Security.Claims;
using System.Text;

namespace Portivio.API.Extensions;

public static class AuthenticationExtensions
{
    public static IServiceCollection AddJwtAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        var jwtSettings = configuration.GetSection(AppSettingsOptions.SectionName).Get<AppSettingsOptions>() ?? new AppSettingsOptions();
        if (string.IsNullOrWhiteSpace(jwtSettings.Key))
            throw new InvalidOperationException("JWT Key is missing in configuration");

        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Key)),
                ValidIssuer = jwtSettings.Issuer,
                ValidAudience = jwtSettings.Audience,
                ClockSkew = TimeSpan.Zero
            };
            options.Events = new JwtBearerEvents
            {
                OnAuthenticationFailed = ctx =>
                {
                    Log.Warning("JWT authentication failed for {RequestPath}: {ErrorMessage}",
                        ctx.HttpContext.Request.Path,
                        ctx.Exception.Message);
                    return Task.CompletedTask;
                },
                OnTokenValidated = ctx =>
                {
                    var userId = ctx.Principal?.FindFirstValue(ClaimTypes.NameIdentifier)
                              ?? ctx.Principal?.FindFirstValue("sub")
                              ?? "unknown";
                    Log.Debug("JWT token validated for UserId={UserId} on {RequestPath}",
                        userId, ctx.HttpContext.Request.Path);
                    return Task.CompletedTask;
                }
            };
        });

        return services;
    }
}
