using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Portivio.Domain.Entities;

namespace Portivio.Infrastructure.Data.Configurations
{
    public class AuthProviderConfiguration : IEntityTypeConfiguration<AuthProvider>
    {
        public void Configure(EntityTypeBuilder<AuthProvider> builder)
        {
            builder.HasKey(a => a.Id);
            builder.HasIndex(a => new { a.Provider, a.ProviderUserId }).IsUnique();
            builder.HasIndex(a => a.UserId);
        }
    }
}