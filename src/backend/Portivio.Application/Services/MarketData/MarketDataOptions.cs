namespace Portivio.Application.Services.MarketData
{
    public class MarketDataOptions
    {
        public const string SectionName = "MarketData";

        public AmfiOptions Amfi { get; set; } = new();
        public AlphaVantageOptions AlphaVantage { get; set; } = new();
        public StandardRatesOptions StandardRates { get; set; } = new();
    }

    public class AmfiOptions
    {
        public string NavUrl { get; set; } = "https://portal.amfiindia.com/spages/NAVAll.txt";
        public int TimeoutSeconds { get; set; } = 60;
    }

    public class AlphaVantageOptions
    {
        public string BaseUrl { get; set; } = "https://www.alphavantage.co";
        public string? ApiKey { get; set; }
        public int TimeoutSeconds { get; set; } = 30;
    }

    public class StandardRatesOptions
    {
        public decimal PpfRatePercent { get; set; } = 7.1m;
        public string PpfSource { get; set; } = "GOVT";
        public List<FdRateConfig> FdRates { get; set; } = new();
    }

    public class FdRateConfig
    {
        public string Bank { get; set; } = string.Empty;
        public int TenureMonths { get; set; }
        public decimal RatePercent { get; set; }
    }
}
