using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Portivio.Domain.Entities;

namespace Portivio.Infrastructure.Data.Configurations
{
    public class EmailSummaryPreferenceConfiguration : IEntityTypeConfiguration<EmailSummaryPreference>
    {
        public void Configure(EntityTypeBuilder<EmailSummaryPreference> builder)
        {
            builder.HasKey(p => p.Id);

            builder.HasIndex(p => p.UserId).IsUnique();
            builder.HasIndex(p => p.IsEnabled);
            builder.HasIndex(p => p.NextRunAtUtc);
            builder.HasIndex(p => p.LockedUntilUtc);

            builder.Property(p => p.TimeZoneId)
                .IsRequired()
                .HasMaxLength(128);

            builder.Property(p => p.TimeOfDay)
                .HasColumnType("time without time zone");

            builder.Property(p => p.LastSendError)
                .HasMaxLength(1024);

            builder.HasOne(p => p.User)
                .WithOne(u => u.EmailSummaryPreference)
                .HasForeignKey<EmailSummaryPreference>(p => p.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
