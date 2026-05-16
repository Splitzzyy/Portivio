using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Portivio.Domain.Entities;

namespace Portivio.Infrastructure.Data.Configurations
{
    public class PriceHistoryConfiguration : IEntityTypeConfiguration<PriceHistory>
    {
        public void Configure(EntityTypeBuilder<PriceHistory> builder)
        {
            builder.HasKey(p => p.Id);
            builder.HasIndex(p => p.InstrumentId);
            builder.HasIndex(p => new { p.InstrumentId, p.Date })
                .IsUnique()
                .HasDatabaseName("idx_pricehistory_instrument_date_unique");
            builder.Property(p => p.Price).HasPrecision(18, 4);
        }
    }
}