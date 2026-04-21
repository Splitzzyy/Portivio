using Microsoft.Extensions.Options;

namespace Portivio.Application.Services.MarketData
{
    public class ConfigStandardRateProvider : IStandardRateProvider
    {
        private readonly MarketDataOptions _options;

        public ConfigStandardRateProvider(IOptions<MarketDataOptions> options)
        {
            _options = options.Value;
        }

        public Task<PpfRateEntry> GetPpfRateAsync(CancellationToken ct = default)
        {
            var rate = _options.StandardRates.PpfRatePercent;
            var source = string.IsNullOrWhiteSpace(_options.StandardRates.PpfSource) ? "GOVT" : _options.StandardRates.PpfSource;
            return Task.FromResult(new PpfRateEntry(rate, DateTime.UtcNow.Date, source));
        }

        public Task<IReadOnlyList<FdRateEntry>> GetFdRatesAsync(CancellationToken ct = default)
        {
            var today = DateTime.UtcNow.Date;
            IReadOnlyList<FdRateEntry> rates = _options.StandardRates.FdRates
                .Where(r => !string.IsNullOrWhiteSpace(r.Bank) && r.TenureMonths > 0 && r.RatePercent > 0)
                .Select(r => new FdRateEntry(
                    r.Bank.Trim(),
                    r.TenureMonths,
                    r.RatePercent,
                    today,
                    $"BANK:{r.Bank.Trim().ToUpperInvariant()}"))
                .ToList();

            return Task.FromResult(rates);
        }
    }
}
