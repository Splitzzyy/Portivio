using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;

namespace Portivio.API.Extensions;

public static class RateLimitingExtensions
{
    public static IServiceCollection AddPortivioRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.AddFixedWindowLimiter("global", opt =>
            {
                opt.PermitLimit = 100;
                opt.Window = TimeSpan.FromMinutes(1);
                opt.QueueLimit = 0;
            });

            options.AddFixedWindowLimiter("login", opt =>
            {
                opt.PermitLimit = 5;
                opt.Window = TimeSpan.FromMinutes(1);
            });

            options.AddPolicy("per-user", context =>
            {
                var userId =
                    context.User.FindFirst("sub")?.Value
                    ?? context.User.FindFirst("userId")?.Value
                    ?? context.Connection.RemoteIpAddress?.ToString();

                return RateLimitPartition.GetFixedWindowLimiter(
                    userId!,
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 60,
                        Window = TimeSpan.FromMinutes(1)
                    });
            });

            options.AddFixedWindowLimiter("fixed", opt =>
            {
                opt.Window = TimeSpan.FromMinutes(1);
                opt.PermitLimit = 5;
                opt.QueueLimit = 0;
            });
        });

        return services;
    }
}
