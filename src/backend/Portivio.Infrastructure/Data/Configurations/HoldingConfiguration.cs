using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Portivio.Domain.Entities;
using System.Text.Json;

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
            builder.Property(h => h.RealizedPnL).HasPrecision(18, 4);
            builder.Property(h => h.AccruedInterest).HasPrecision(18, 4);

            var snapshotConverter = new ValueConverter<JsonDocument?, string?>(
                v => v == null ? null : v.RootElement.GetRawText(),
                v => v == null ? null : JsonDocument.Parse(v));
            var snapshotComparer = new ValueComparer<JsonDocument?>(
                (a, b) => (a == null && b == null) ||
                          (a != null && b != null && a.RootElement.GetRawText() == b.RootElement.GetRawText()),
                v => v == null ? 0 : v.RootElement.GetRawText().GetHashCode(),
                v => v == null ? null : JsonDocument.Parse(v.RootElement.GetRawText()));
            builder.Property(h => h.Snapshot)
                .HasColumnType("jsonb")
                .HasConversion(snapshotConverter, snapshotComparer);
        }
    }
}