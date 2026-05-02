using Microsoft.AspNetCore.HttpOverrides;

namespace Portivio.API.Extensions;

public static class InfrastructureExtensions
{
    public const string FrontendCorsPolicy = "FrontendDevelopment";

    public static IServiceCollection AddForwardedHeadersConfiguration(this IServiceCollection services)
    {
        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            options.KnownIPNetworks.Clear();
            options.KnownProxies.Clear();
        });
        return services;
    }

    public static IServiceCollection AddCorsPolicy(this IServiceCollection services, IWebHostEnvironment environment, IConfiguration configuration)
    {
        services.AddCors(options =>
        {
            options.AddPolicy(FrontendCorsPolicy, policy =>
            {
                var allowedOrigins = environment.IsDevelopment()
                    ? ["http://localhost:4200"]
                    : configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];

                if (allowedOrigins.Length == 0)
                    return;
                policy
                    .WithOrigins(allowedOrigins)
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                    .AllowCredentials();
            });
        });
        return services;
    }
}
