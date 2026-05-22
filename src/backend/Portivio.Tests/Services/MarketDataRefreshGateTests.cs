using Portivio.Application.Services.MarketData;
using Xunit;

namespace Portivio.Tests.Services
{
    public class MarketDataRefreshGateTests
    {
        [Fact]
        public async Task RunAsync_SerializesConcurrentWork_ForSameKey()
        {
            var gate = new MarketDataRefreshGate();
            var running = 0;
            var maxRunning = 0;

            async Task<int> Work(CancellationToken ct)
            {
                var current = Interlocked.Increment(ref running);
                maxRunning = Math.Max(maxRunning, current);
                await Task.Delay(25, ct);
                Interlocked.Decrement(ref running);
                return current;
            }

            await Task.WhenAll(
                gate.RunAsync("stock:TCS.NS", Work),
                gate.RunAsync("stock:TCS.NS", Work),
                gate.RunAsync("stock:TCS.NS", Work));

            Assert.Equal(1, maxRunning);
        }
    }
}
