using Microsoft.Extensions.Options;
using Portivio.Application.Services.MarketData;
using Xunit;

namespace Portivio.Tests.Services
{
    public class GoldRateProviderTests
    {
        private static IGoldRateProvider Build(decimal rate24K, decimal multiplier22K = 0.9167m)
        {
            var options = new MarketDataOptions
            {
                Gold = new GoldOptions { RatePerGram24K = rate24K, Purity22KMultiplier = multiplier22K }
            };
            var monitor = new TestOptionsMonitor<MarketDataOptions>(options);
            return new GoldRateProvider(monitor);
        }

        [Fact]
        public async Task Returns_Configured_Rate_For_24K()
        {
            var provider = Build(rate24K: 7480m);
            var rate = await provider.GetRatePerGramAsync("24K");
            Assert.Equal(7480m, rate);
        }

        [Fact]
        public async Task Returns_Multiplier_Adjusted_Rate_For_22K()
        {
            var provider = Build(rate24K: 7480m, multiplier22K: 0.9167m);
            var rate = await provider.GetRatePerGramAsync("22K");
            Assert.Equal(7480m * 0.9167m, rate);
        }

        [Fact]
        public async Task Returns_Null_For_Unknown_Purity()
        {
            var provider = Build(rate24K: 7480m);
            Assert.Null(await provider.GetRatePerGramAsync("18K"));
            Assert.Null(await provider.GetRatePerGramAsync(""));
        }

        [Fact]
        public async Task Returns_Null_When_Rate_Not_Configured()
        {
            var provider = Build(rate24K: 0m);
            Assert.Null(await provider.GetRatePerGramAsync("24K"));
            Assert.Null(await provider.GetRatePerGramAsync("22K"));
        }

        [Fact]
        public async Task Purity_Is_Case_Insensitive()
        {
            var provider = Build(rate24K: 7480m);
            Assert.Equal(7480m, await provider.GetRatePerGramAsync("24k"));
            Assert.Equal(7480m, await provider.GetRatePerGramAsync(" 24K "));
        }

        private sealed class TestOptionsMonitor<T> : IOptionsMonitor<T>
        {
            public TestOptionsMonitor(T value) { CurrentValue = value; }
            public T CurrentValue { get; }
            public T Get(string? name) => CurrentValue;
            public IDisposable? OnChange(Action<T, string?> listener) => null;
        }
    }
}
