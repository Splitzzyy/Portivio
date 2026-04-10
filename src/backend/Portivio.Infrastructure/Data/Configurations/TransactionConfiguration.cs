using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Portivio.Domain.Entities;

namespace Portivio.Infrastructure.Data.Configurations
{
    public class TransactionConfiguration : IEntityTypeConfiguration<Transaction>
    {
        public void Configure(EntityTypeBuilder<Transaction> builder)
        {
            builder.HasKey(t => t.Id);
            builder.HasIndex(t => t.ProfileId);
            builder.HasIndex(t => t.InstrumentId);
            builder.HasIndex(t => new { t.ProfileId, t.InstrumentId }).HasDatabaseName("idx_transactions_profile_instrument");
            builder.Property(t => t.Price).HasPrecision(18, 4);
            builder.Property(t => t.Amount).HasPrecision(18, 4);
            builder.Property(t => t.Quantity).HasPrecision(18, 4);
            builder.Property(t => t.Type).HasConversion<int>();
        }
    }
}