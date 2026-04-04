namespace Portivio.Domain.Entities
{
    public class PriceHistory
    {
        public Guid Id { get; set; }
        public Guid InstrumentId { get; set; }
        public decimal Price { get; set; }
        public DateTime Date { get; set; }
        public string Source { get; set; } = null!;
        public DateTime CreatedAt { get; set; }
        public Instrument Instrument { get; set; } = null!;
    }
}