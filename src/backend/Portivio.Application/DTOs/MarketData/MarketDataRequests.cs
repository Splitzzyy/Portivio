namespace Portivio.Application.DTOs.MarketData
{
    public class SyncSummaryResponse
    {
        public int Inserted { get; set; }
        public int Skipped { get; set; }
        public int CreatedInstruments { get; set; }
        public List<string> Errors { get; set; } = new();
    }

    public class StockPriceResponse
    {
        public string Symbol { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public DateTime AsOf { get; set; }
        public string Source { get; set; } = string.Empty;
    }

    public class MutualFundNavResponse
    {
        public string Isin { get; set; } = string.Empty;
        public string SchemeName { get; set; } = string.Empty;
        public decimal Nav { get; set; }
        public DateTime AsOf { get; set; }
        public string Source { get; set; } = string.Empty;
    }

    public class FdRateResponse
    {
        public string Bank { get; set; } = string.Empty;
        public int TenureMonths { get; set; }
        public decimal RatePercent { get; set; }
        public DateTime AsOf { get; set; }
        public string Source { get; set; } = string.Empty;
    }

    public class PpfRateResponse
    {
        public decimal RatePercent { get; set; }
        public DateTime AsOf { get; set; }
        public string Source { get; set; } = string.Empty;
    }

    public class UpsertFdRateRequest
    {
        public string Bank { get; set; } = string.Empty;
        public int TenureMonths { get; set; }
        public decimal RatePercent { get; set; }
    }
}
