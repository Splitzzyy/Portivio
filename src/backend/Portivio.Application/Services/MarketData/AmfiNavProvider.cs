using System.Globalization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Portivio.Application.Services.MarketData
{
    public class AmfiNavProvider : IMutualFundNavProvider
    {
        public const string HttpClientName = "AmfiNav";
        private const string SourceTag = "AMFI";

        private readonly IHttpClientFactory _httpClientFactory;
        private readonly MarketDataOptions _options;
        private readonly ILogger<AmfiNavProvider> _logger;

        public AmfiNavProvider(
            IHttpClientFactory httpClientFactory,
            IOptions<MarketDataOptions> options,
            ILogger<AmfiNavProvider> logger)
        {
            _httpClientFactory = httpClientFactory;
            _options = options.Value;
            _logger = logger;
        }

        public async Task<IReadOnlyList<MutualFundNav>> GetAllNavsAsync(CancellationToken ct = default)
        {
            var client = _httpClientFactory.CreateClient(HttpClientName);
            var content = await client.GetStringAsync(_options.Amfi.NavUrl, ct);
            return Parse(content);
        }

        public async Task<MutualFundNav?> GetByIsinAsync(string isin, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(isin))
                return null;

            var all = await GetAllNavsAsync(ct);
            return all.FirstOrDefault(n => string.Equals(n.Isin, isin, StringComparison.OrdinalIgnoreCase));
        }

        private IReadOnlyList<MutualFundNav> Parse(string csv)
        {
            // AMFI NAVAll.txt format: semicolon-delimited.
            // Columns: Scheme Code;ISIN Div Payout/ ISIN Growth;ISIN Div Reinvestment;Scheme Name;Net Asset Value;Date
            var results = new List<MutualFundNav>();
            var lines = csv.Split('\n');

            foreach (var raw in lines)
            {
                var line = raw.Trim();
                if (line.Length == 0) continue;
                if (!line.Contains(';')) continue; // skip headers/category labels

                var cols = line.Split(';');
                if (cols.Length < 6) continue;

                var isinGrowth = cols[1].Trim();
                var isinReinvest = cols[2].Trim();
                var schemeName = cols[3].Trim();
                var navStr = cols[4].Trim();
                var dateStr = cols[5].Trim();

                if (!decimal.TryParse(navStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var nav))
                    continue;

                if (!DateTime.TryParseExact(dateStr, "dd-MMM-yyyy", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var date))
                    continue;

                if (!string.IsNullOrWhiteSpace(isinGrowth) && isinGrowth != "-")
                    results.Add(new MutualFundNav(isinGrowth, schemeName, nav, date, SourceTag));

                if (!string.IsNullOrWhiteSpace(isinReinvest) && isinReinvest != "-" && !string.Equals(isinReinvest, isinGrowth, StringComparison.OrdinalIgnoreCase))
                    results.Add(new MutualFundNav(isinReinvest, schemeName, nav, date, SourceTag));
            }

            _logger.LogInformation("AMFI NAV parse: {Count} entries", results.Count);
            return results;
        }
    }
}
