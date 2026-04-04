using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Portivio.Domain.Entities;

namespace Portivio.Infrastructure.Data.Configurations
{
    public class ProfileConfiguration : IEntityTypeConfiguration<Profile>
    {
        public void Configure(EntityTypeBuilder<Profile> builder)
        {
            builder.HasKey(p => p.Id);
            builder.HasIndex(p => p.UserId);
            builder.HasMany(p => p.Transactions)
                .WithOne(t => t.Profile)
                .HasForeignKey(t => t.ProfileId);
            builder.HasMany(p => p.Holdings)
                .WithOne(h => h.Profile)
                .HasForeignKey(h => h.ProfileId);
            builder.HasMany(p => p.SIPPlans)
                .WithOne(s => s.Profile)
                .HasForeignKey(s => s.ProfileId);
            builder.HasMany(p => p.PortfolioPerformances)
                .WithOne(pp => pp.Profile)
                .HasForeignKey(pp => pp.ProfileId);
        }
    }
}