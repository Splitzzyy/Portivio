using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Portivio.Domain.Entities;
using System.Text.Json;

namespace Portivio.Infrastructure.Data.Configurations
{
    public class InstrumentConfiguration : IEntityTypeConfiguration<Instrument>
    {
        public void Configure(EntityTypeBuilder<Instrument> builder)
        {
            builder.HasKey(i => i.Id);
            builder.Property(i => i.Category).HasConversion<int>();
            builder.Property(i => i.PriceSource).HasConversion<int>();

            var metadataConverter = new ValueConverter<JsonDocument?, string?>(
                v => v == null ? null : v.RootElement.GetRawText(),
                v => v == null ? null : JsonDocument.Parse(v));
            var metadataComparer = new ValueComparer<JsonDocument?>(
                (a, b) => (a == null && b == null) ||
                          (a != null && b != null && a.RootElement.GetRawText() == b.RootElement.GetRawText()),
                v => v == null ? 0 : v.RootElement.GetRawText().GetHashCode(),
                v => v == null ? null : JsonDocument.Parse(v.RootElement.GetRawText()));
            builder.Property(i => i.Metadata)
                .HasColumnType("jsonb")
                .HasConversion(metadataConverter, metadataComparer);
            builder.HasIndex(i => i.AssetTypeId);
            builder.HasIndex(i => i.Isin).HasDatabaseName("ix_instruments_isin");
            builder.HasIndex(i => new { i.AssetTypeId, i.Symbol })
                .IsUnique()
                .HasDatabaseName("ux_instruments_assettype_symbol");
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