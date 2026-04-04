using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Portivio.Domain.Entities;

namespace Portivio.Infrastructure.Data.Configurations
{
    public class InstrumentConfiguration : IEntityTypeConfiguration<Instrument>
    {
        public void Configure(EntityTypeBuilder<Instrument> builder)
        {
            builder.HasKey(i => i.Id);
            builder.HasIndex(i => i.AssetTypeId);
            builder.HasMany(i => i.Transactions)
                .WithOne(t => t.Instrument)
                .HasForeignKey(t => t.InstrumentId);
            builder.HasMany(i => i.Holdings)
                .WithOne(h => h.Instrument)
                .HasForeignKey(h => h.InstrumentId);
            builder.HasMany(i => i.PriceHistories)
                .WithOne(ph => ph.Instrument)
                .HasForeignKey(ph => ph.InstrumentId);
            builder.HasMany(i => i.SIPPlans)
                .WithOne(s => s.Instrument)
                .HasForeignKey(s => s.InstrumentId);
        }
    }
}