using Microsoft.EntityFrameworkCore;
using Portivio.Application.Services;
using Portivio.Infrastructure.Data;

namespace Portivio.API.Extensions;

public static class DatabaseExtensions
{
    public static IServiceCollection AddDatabase(this IServiceCollection services, IConfiguration configuration)
    {
        var options = configuration.GetSection(PostgresOptions.SectionName).Get<PostgresOptions>() ?? new PostgresOptions();
        if (string.IsNullOrWhiteSpace(options.ConnectionString))
            throw new InvalidOperationException("Postgres connection string missing");

        services.AddDbContext<PortivioDbContext>(opt => opt.UseNpgsql(options.ConnectionString));
        return services;
    }
}
