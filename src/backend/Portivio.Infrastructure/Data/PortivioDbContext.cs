using Microsoft.EntityFrameworkCore;
using Portivio.Domain.Entities;
using System.Reflection;

namespace Portivio.Infrastructure.Data
{
    public class PortivioDbContext : DbContext
    {
        public PortivioDbContext(DbContextOptions<PortivioDbContext> options) : base(options) { }

        public DbSet<User> Users { get; set; }
        public DbSet<Profile> Profiles { get; set; }
        public DbSet<AuthProvider> AuthProviders { get; set; }
        public DbSet<AuthToken> AuthTokens { get; set; }
        public DbSet<AuditLog> AuditLogs { get; set; }
        public DbSet<PortfolioPerformance> PortfolioPerformances { get; set; }
        public DbSet<Transaction> Transactions { get; set; }
        public DbSet<Instrument> Instruments { get; set; }
        public DbSet<AssetType> AssetTypes { get; set; }
        public DbSet<Holding> Holdings { get; set; }
        public DbSet<PriceHistory> PriceHistories { get; set; }
        public DbSet<SIPPlan> SIPPlans { get; set; }
        public DbSet<EmailSummaryPreference> EmailSummaryPreferences { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
        }
    }
}
