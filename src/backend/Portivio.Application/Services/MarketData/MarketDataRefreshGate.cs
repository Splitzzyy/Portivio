using System.Collections.Concurrent;

namespace Portivio.Application.Services.MarketData
{
    public interface IMarketDataRefreshGate
    {
        Task<T> RunAsync<T>(string key, Func<CancellationToken, Task<T>> action, CancellationToken ct = default);
    }

    public sealed class MarketDataRefreshGate : IMarketDataRefreshGate
    {
        private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new(StringComparer.OrdinalIgnoreCase);

        public async Task<T> RunAsync<T>(string key, Func<CancellationToken, Task<T>> action, CancellationToken ct = default)
        {
            var normalizedKey = string.IsNullOrWhiteSpace(key) ? "default" : key.Trim().ToUpperInvariant();
            var gate = _locks.GetOrAdd(normalizedKey, _ => new SemaphoreSlim(1, 1));

            await gate.WaitAsync(ct);
            try
            {
                return await action(ct);
            }
            finally
            {
                gate.Release();
            }
        }
    }
}
