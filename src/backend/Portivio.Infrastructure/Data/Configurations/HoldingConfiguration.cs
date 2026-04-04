using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Portivio.Domain.Entities;

namespace Portivio.Infrastructure.Data.Configurations
{
    public class HoldingConfiguration : IEntityTypeConfiguration<Holding>
    {
        public void Configure(EntityTypeBuilder<Holding> builder)
        {
            builder.HasKey(h => h.Id);
            builder.HasIndex(h => h.ProfileId);
            builder.HasIndex(h => h.InstrumentId);
            builder.Property(h => h.Quantity).HasPrecision(18, 4);
            builder.Property(h => h.AvgPrice).HasPrecision(18, 4);
            builder.Property(h => h.CurrentPrice).HasPrecision(18, 4);
            builder.Property(h => h.MarketValue).HasPrecision(18, 4);
            builder.Property(h => h.UnrealizedPnL).HasPrecision(18, 4);
        }
    }
}