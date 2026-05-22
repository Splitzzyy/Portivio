using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging.Abstractions;
using Portivio.Application.Services.MarketData;
using System.Net;
using Xunit;

namespace Portivio.Tests.Services
{
    public class GoldRateProviderTests
    {
        private const decimal XauInrPrice = 232700.488m;

        private static IGoldRateProvider Build(
            string json = "{\"price\":232700.488}",
            HttpStatusCode statusCode = HttpStatusCode.OK,
            decimal purity22KMultiplier = 0.916m)
        {
            var options = new MarketDataOptions
            {
                Gold = new GoldOptions
                {
                    PriceUrl = "https://api.gold-api.com/price/XAU/INR",
                    TroyOunceGrams = 31.1035m,
                    Purity22KMultiplier = purity22KMultiplier
                }
            };
            var monitor = new TestOptionsMonitor<MarketDataOptions>(options);
            var factory = new TestHttpClientFactory(new HttpClient(new TestHandler(json, statusCode)));
            return new GoldRateProvider(monitor, factory, NullLogger<GoldRateProvider>.Instance);
        }

        [Fact]
        public async Task Returns_Api_Price_Converted_To_Per_Gram_For_24K()
        {
            var provider = Build();
            var rate = await provider.GetRatePerGramAsync("24K");
            Assert.Equal(XauInrPrice / 31.1035m, rate);
        }

        [Fact]
        public async Task Returns_22K_Rate_Using_Configured_Multiplier()
        {
            var provider = Build(purity22KMultiplier: 0.916m);
            var rate = await provider.GetRatePerGramAsync("22K");
            Assert.Equal((XauInrPrice / 31.1035m) * 0.916m, rate);
        }

        [Fact]
        public async Task Returns_Null_For_Unsupported_Purity()
        {
            var provider = Build();
            Assert.Null(await provider.GetRatePerGramAsync("18K"));
            Assert.Null(await provider.GetRatePerGramAsync(""));
        }

        [Fact]
        public async Task Returns_Null_When_Api_Price_Is_Not_Usable()
        {
            var provider = Build(json: "{\"price\":0}");
            Assert.Null(await provider.GetRatePerGramAsync("24K"));
            Assert.Null(await provider.GetRatePerGramAsync("22K"));
        }

        private sealed class TestOptionsMonitor<T> : IOptionsMonitor<T>
        {
            public TestOptionsMonitor(T value) { CurrentValue = value; }
            public T CurrentValue { get; }
            public T Get(string? name) => CurrentValue;
            public IDisposable? OnChange(Action<T, string?> listener) => null;
        }

        private sealed class TestHttpClientFactory : IHttpClientFactory
        {
            private readonly HttpClient _client;

            public TestHttpClientFactory(HttpClient client)
            {
                _client = client;
            }

            public HttpClient CreateClient(string name) => _client;
        }

        private sealed class TestHandler : HttpMessageHandler
        {
            private readonly string _json;
            private readonly HttpStatusCode _statusCode;

            public TestHandler(string json, HttpStatusCode statusCode)
            {
                _json = json;
                _statusCode = statusCode;
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                return Task.FromResult(new HttpResponseMessage(_statusCode)
                {
                    Content = new StringContent(_json)
                });
            }
        }
    }
}
