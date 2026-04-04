namespace Portivio.Domain.Entities
{
    public class AuditLog
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public string Action { get; set; } = null!;
        public string Entity { get; set; } = null!;
        public Guid EntityId { get; set; }
        public string OldValues { get; set; } = null!; // JSON
        public string NewValues { get; set; } = null!; // JSON
        public string IpAddress { get; set; } = null!;
        public string UserAgent { get; set; } = null!;
        public string CorrelationId { get; set; } = null!;
        public DateTime CreatedAt { get; set; }
        public User User { get; set; } = null!;
    }
}