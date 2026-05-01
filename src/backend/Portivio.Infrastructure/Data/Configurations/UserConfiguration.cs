using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Portivio.Domain.Entities;

namespace Portivio.Infrastructure.Data.Configurations
{
    public class UserConfiguration : IEntityTypeConfiguration<User>
    {
        public void Configure(EntityTypeBuilder<User> builder)
        {
            builder.HasKey(u => u.Id);
            builder.HasIndex(u => u.Email).IsUnique();
            builder.Property(u => u.PasswordHash)
                .HasMaxLength(255);
            builder.Property(u => u.EmailVerificationToken)
                .HasMaxLength(512);
            builder.Property(u => u.PasswordResetToken)
                .HasMaxLength(512);
            builder.HasMany(u => u.Profiles)
                .WithOne(p => p.User)
                .HasForeignKey(p => p.UserId);
            builder.HasMany(u => u.AuthProviders)
                .WithOne()
                .HasForeignKey(a => a.UserId);
            builder.HasMany(u => u.AuditLogs)
                .WithOne(a => a.User)
                .HasForeignKey(a => a.UserId);
        }
    }
}
