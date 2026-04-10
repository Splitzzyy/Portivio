namespace Portivio.Domain.Entities
{
    public class AuthToken
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public string AccessTokenHash { get; set; } = null!;
        public string RefreshTokenHash { get; set; } = null!;
        public DateTime AccessTokenExpiry { get; set; }
        public DateTime RefreshTokenExpiry { get; set; }
        public string DeviceInfo { get; set; } = null!;
        public string IpAddress { get; set; } = null!;
        public bool Revoked { get; set; }
        public DateTime CreatedAt { get; set; }

        // Navigation property
        public User User { get; set; } = null!;
    }
}