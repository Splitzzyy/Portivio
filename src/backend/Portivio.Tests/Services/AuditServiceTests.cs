using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Portivio.Domain.Entities;
using Portivio.Infrastructure.Data;
using Portivio.Infrastructure.Services.Audit;
using System.Net;
using Xunit;

namespace Portivio.Tests.Services
{
    public class AuditServiceTests
    {
        private static PortivioDbContext CreateInMemoryDbContext()
        {
            var options = new DbContextOptionsBuilder<PortivioDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            return new PortivioDbContext(options);
        }

        [Fact]
        public async Task LogAsync_CapturesMetadata_Serializes_AndPersists()
        {
            // Arrange
            var context = CreateInMemoryDbContext();

            var httpContext = new DefaultHttpContext();
            httpContext.Connection.RemoteIpAddress = IPAddress.Parse("203.0.113.10");
            httpContext.Request.Headers.UserAgent = "Portivio.Tests/1.0";
            httpContext.Request.Headers["X-Correlation-ID"] = "corr-123";

            var httpContextAccessor = new HttpContextAccessor { HttpContext = httpContext };
            var service = new AuditService(context, httpContextAccessor);

            var userId = Guid.NewGuid();
            var entityId = Guid.NewGuid();

            // Act
            await service.LogAsync(
                userId: userId,
                action: "Auth_Login_Success",
                entity: "User",
                entityId: entityId,
                oldValues: new { A = 1 },
                newValues: new { B = 2 });

            // Assert
            var auditLog = await context.AuditLogs.SingleAsync();
            Assert.Equal(userId, auditLog.UserId);
            Assert.Equal("Auth_Login_Success", auditLog.Action);
            Assert.Equal("User", auditLog.Entity);
            Assert.Equal(entityId, auditLog.EntityId);
            Assert.Equal("203.0.113.10", auditLog.IpAddress);
            Assert.Equal("Portivio.Tests/1.0", auditLog.UserAgent);
            Assert.Equal("corr-123", auditLog.CorrelationId);
            Assert.Contains("\"A\":1", auditLog.OldValues);
            Assert.Contains("\"B\":2", auditLog.NewValues);
            Assert.NotEqual(default, auditLog.CreatedAt);
            Assert.NotEqual(Guid.Empty, auditLog.Id);
        }

        [Fact]
        public async Task LogAsync_WhenHttpContextIsNull_DoesNotThrow_AndPersists()
        {
            // Arrange
            var context = CreateInMemoryDbContext();
            var httpContextAccessor = new HttpContextAccessor { HttpContext = null };
            var service = new AuditService(context, httpContextAccessor);

            // Act
            await service.LogAsync(
                userId: Guid.NewGuid(),
                action: "Auth_Login_Success",
                entity: "User",
                entityId: Guid.NewGuid(),
                oldValues: null,
                newValues: null);

            // Assert
            var auditLog = await context.AuditLogs.SingleAsync();
            Assert.NotNull(auditLog.IpAddress);
            Assert.NotNull(auditLog.UserAgent);
            Assert.NotNull(auditLog.CorrelationId);
            Assert.NotNull(auditLog.OldValues);
            Assert.NotNull(auditLog.NewValues);
        }
    }
}

