namespace Portivio.Domain.Entities
{
    public class AuthProvider
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public string Provider { get; set; } = null!;
        public string ProviderUserId { get; set; } = null!;
        public string Email { get; set; } = null!;
        public DateTime CreatedAt { get; set; }
    }
}