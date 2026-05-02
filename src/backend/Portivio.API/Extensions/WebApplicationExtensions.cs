using Microsoft.EntityFrameworkCore;
using Portivio.Infrastructure.Data;

namespace Portivio.API.Extensions;

public static class WebApplicationExtensions
{
    public static async Task RunWithMigrationAsync(this WebApplication app)
    {
        using (var scope = app.Services.CreateScope())
        {
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
            try
            {
                logger.LogInformation("[Startup] Running EF Core migrations...");
                var db = scope.ServiceProvider.GetRequiredService<PortivioDbContext>();
                db.Database.Migrate();
                logger.LogInformation("[Startup] Migrations OK.");
            }
            catch (Exception ex)
            {
                logger.LogCritical(ex, "[Startup] FAILED: EF Core migration error. Check DB connection and schema.");
                throw;
            }
        }

        await app.RunAsync();
    }
}
