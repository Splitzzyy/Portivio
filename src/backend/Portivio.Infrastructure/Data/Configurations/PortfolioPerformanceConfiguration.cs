using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Portivio.Domain.Entities;

namespace Portivio.Infrastructure.Data.Configurations
{
    public class PortfolioPerformanceConfiguration : IEntityTypeConfiguration<PortfolioPerformance>
    {
        public void Configure(EntityTypeBuilder<PortfolioPerformance> builder)
        {
            builder.HasKey(p => p.Id);
            builder.HasIndex(p => p.ProfileId);
            builder.Property(p => p.TotalInvestment).HasPrecision(18, 4);
            builder.Property(p => p.CurrentValue).HasPrecision(18, 4);
            builder.Property(p => p.DayChange).HasPrecision(18, 4);
            builder.Property(p => p.TotalReturn).HasPrecision(18, 4);
            builder.Property(p => p.XIRR).HasPrecision(18, 4);
        }
    }
}