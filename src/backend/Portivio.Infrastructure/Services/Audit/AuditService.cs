using Microsoft.AspNetCore.Http;
using Portivio.Domain.Entities;
using Portivio.Domain.Services.Audit;
using Portivio.Infrastructure.Data;
using System.Text.Json;

namespace Portivio.Infrastructure.Services.Audit
{
    public class AuditService : IAuditService
    {
        private readonly PortivioDbContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public AuditService(PortivioDbContext context, IHttpContextAccessor httpContextAccessor)
        {
            _context = context;
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task LogAsync(
            Guid userId,
            string action,
            string entity,
            Guid entityId,
            object? oldValues = null,
            object? newValues = null)
        {
            var httpContext = _httpContextAccessor.HttpContext;

            var ipAddress = httpContext?.Connection?.RemoteIpAddress?.ToString() ?? string.Empty;
            var userAgent = httpContext?.Request?.Headers.UserAgent.ToString() ?? string.Empty;

            var correlationIdHeader = httpContext?.Request?.Headers["X-Correlation-ID"].ToString();
            var correlationId = string.IsNullOrWhiteSpace(correlationIdHeader)
                ? (httpContext?.TraceIdentifier ?? string.Empty)
                : correlationIdHeader;

            var auditLog = new AuditLog
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Action = action,
                Entity = entity,
                EntityId = entityId,
                OldValues = SerializeOrEmptyObject(oldValues),
                NewValues = SerializeOrEmptyObject(newValues),
                IpAddress = ipAddress,
                UserAgent = userAgent,
                CorrelationId = correlationId,
                CreatedAt = DateTime.UtcNow
            };

            _context.AuditLogs.Add(auditLog);
            await _context.SaveChangesAsync();
        }

        private static string SerializeOrEmptyObject(object? values)
        {
            if (values == null)
            {
                return "{}";
            }

            return JsonSerializer.Serialize(values);
        }
    }
}
