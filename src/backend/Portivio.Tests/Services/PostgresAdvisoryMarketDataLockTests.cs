using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Portivio.Application.Services.MarketData;
using Portivio.Infrastructure.Data;
using Xunit;

namespace Portivio.Tests.Services
{
    public class PostgresAdvisoryMarketDataLockTests
    {
        [Fact]
        public async Task RunAsync_WhenProviderIsNotPostgres_RunsActionWithoutDatabaseLock()
        {
            var options = new DbContextOptionsBuilder<PortivioDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            await using var context = new PortivioDbContext(options);
            var locker = new PostgresAdvisoryMarketDataLock(
                context,
                Options.Create(new MarketDataOptions()),
                new LoggerFactory().CreateLogger<PostgresAdvisoryMarketDataLock>());

            var called = false;
            var result = await locker.RunAsync("live:TCS.NS", _ =>
            {
                called = true;
                return Task.FromResult(42);
            });

            Assert.True(called);
            Assert.Equal(42, result);
        }

        [Fact]
        public void CreateLockId_IsStable_AndCaseInsensitive()
        {
            var first = PostgresAdvisoryMarketDataLock.CreateLockId("live:TCS.NS");
            var second = PostgresAdvisoryMarketDataLock.CreateLockId(" LIVE:tcs.ns ");

            Assert.Equal(first, second);
        }
    }
}
