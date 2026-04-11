namespace Portivio.Domain.Entities
{
    public class User
    {
        public Guid Id { get; set; }
        public string Email { get; set; } = null!;
        public string Name { get; set; } = null!;
        public string? PasswordHash { get; set; }
        public bool IsVerified { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? LastLoginAt { get; set; }
        public bool IsActive { get; set; }
        public ICollection<Profile> Profiles { get; set; } = new List<Profile>();
        public ICollection<AuthProvider> AuthProviders { get; set; } = new List<AuthProvider>();
        public ICollection<AuthToken> AuthTokens { get; set; } = new List<AuthToken>();
        public ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();
    }
}
