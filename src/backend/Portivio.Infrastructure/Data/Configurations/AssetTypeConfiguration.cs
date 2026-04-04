using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Portivio.Domain.Entities;

namespace Portivio.Infrastructure.Data.Configurations
{
    public class AssetTypeConfiguration : IEntityTypeConfiguration<AssetType>
    {
        public void Configure(EntityTypeBuilder<AssetType> builder)
        {
            builder.HasKey(a => a.Id);
            builder.HasMany(a => a.Instruments)
                .WithOne(i => i.AssetType)
                .HasForeignKey(i => i.AssetTypeId);
        }
    }
}