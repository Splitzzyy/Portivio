using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Portivio.Infrastructure.Data
{
    public class PortivioDbContextFactory : IDesignTimeDbContextFactory<PortivioDbContext>
    {
        public PortivioDbContext CreateDbContext(string[] args)
        {
            var connectionString =
                Environment.GetEnvironmentVariable("POSTGRES_CONNECTION_STRING")
                ?? "Host=localhost;Database=portivio;Username=postgres;Password=postgres";

            var optionsBuilder = new DbContextOptionsBuilder<PortivioDbContext>();
            optionsBuilder.UseNpgsql(connectionString);

            return new PortivioDbContext(optionsBuilder.Options);
        }
    }
}
