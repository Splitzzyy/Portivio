using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Portivio.Domain.Entities;

namespace Portivio.Infrastructure.Data.Configurations
{
    public class SIPPlanConfiguration : IEntityTypeConfiguration<SIPPlan>
    {
        public void Configure(EntityTypeBuilder<SIPPlan> builder)
        {
            builder.HasKey(s => s.Id);
            builder.HasIndex(s => s.ProfileId);
            builder.HasIndex(s => s.InstrumentId);
            builder.Property(s => s.Amount).HasPrecision(18, 4);
        }
    }
}