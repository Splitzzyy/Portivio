namespace Portivio.Application.DTOs.Holding
{
    public class UpsertHoldingRequest
    {
        public Guid InstrumentId { get; set; }
        public decimal Quantity { get; set; }
        public decimal AvgPrice { get; set; }
        public decimal CurrentPrice { get; set; }
    }

    public class HoldingResponse
    {
        public Guid Id { get; set; }
        public Guid ProfileId { get; set; }
        public Guid InstrumentId { get; set; }
        public string InstrumentName { get; set; } = string.Empty;
        public string InstrumentSymbol { get; set; } = string.Empty;
        public string AssetTypeName { get; set; } = string.Empty;
        public string Currency { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public decimal AvgPrice { get; set; }
        public decimal CurrentPrice { get; set; }
        public decimal MarketValue { get; set; }
        public decimal UnrealizedPnL { get; set; }
        public DateTime LastUpdated { get; set; }
    }
}
