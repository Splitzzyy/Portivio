namespace Portivio.Domain.Services.Audit
{
    public interface IAuditService
    {
        Task LogAsync(
            Guid? userId,
            string action,
            string entity,
            Guid entityId,
            object? oldValues = null,
            object? newValues = null);
    }
}

