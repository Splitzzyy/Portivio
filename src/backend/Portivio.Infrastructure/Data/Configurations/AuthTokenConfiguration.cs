using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Portivio.Domain.Entities;

namespace Portivio.Infrastructure.Data.Configurations
{
    public class AuthTokenConfiguration : IEntityTypeConfiguration<AuthToken>
    {
        public void Configure(EntityTypeBuilder<AuthToken> builder)
        {
            builder.HasKey(a => a.Id);
            builder.HasIndex(a => a.UserId);
            builder.HasOne(a => a.User)
                .WithMany(u => u.AuthTokens)
                .HasForeignKey(a => a.UserId);
        }
    }
}
