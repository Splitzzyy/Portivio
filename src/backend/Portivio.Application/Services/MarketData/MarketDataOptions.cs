namespace Portivio.Application.Services.MarketData
{
    public class MarketDataOptions
    {
        public const string SectionName = "MarketData";

        public AmfiOptions Amfi { get; set; } = new();
        public StandardRatesOptions StandardRates { get; set; } = new();
        public GoldOptions Gold { get; set; } = new();
    }

    public class GoldOptions
    {
        public string PriceUrl { get; set; } = "https://api.gold-api.com/price/XAU/INR";
        public int TimeoutSeconds { get; set; } = 10;
        public decimal TroyOunceGrams { get; set; } = 31.1035m;
        public decimal Purity22KMultiplier { get; set; } = 0.916m;
    }

    public class AmfiOptions
    {
        public string NavUrl { get; set; } = "https://portal.amfiindia.com/spages/NAVAll.txt";
        public int TimeoutSeconds { get; set; } = 60;
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
