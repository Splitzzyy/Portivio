using System.Text.Json;

namespace Portivio.Domain.Entities
{
    public class Holding
    {
        public Guid Id { get; set; }
        public Guid ProfileId { get; set; }
        public Guid InstrumentId { get; set; }
        public decimal Quantity { get; set; }
        public decimal AvgPrice { get; set; }
        public decimal CurrentPrice { get; set; }
        public decimal MarketValue { get; set; }
        public decimal UnrealizedPnL { get; set; }
        public decimal RealizedPnL { get; set; }
        public decimal AccruedInterest { get; set; }
        public JsonDocument? Snapshot { get; set; }
        public DateTime LastUpdated { get; set; }
        public Profile Profile { get; set; } = null!;
        public Instrument Instrument { get; set; } = null!;
    }
}