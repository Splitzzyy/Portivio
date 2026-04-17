namespace Portivio.Application.DTOs.PriceHistory
{
    public class AddPriceRequest
    {
        public decimal Price { get; set; }
        public DateTime Date { get; set; }
        public string Source { get; set; } = string.Empty;
    }

    public class BulkAddPriceRequest
    {
        public List<AddPriceRequest> Prices { get; set; } = new();
    }

    public class PriceHistoryResponse
    {
        public Guid Id { get; set; }
        public Guid InstrumentId { get; set; }
        public decimal Price { get; set; }
        public DateTime Date { get; set; }
        public string Source { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }

    public class BulkAddPriceResponse
    {
        public int Inserted { get; set; }
        public int Skipped { get; set; }
        public List<string> Errors { get; set; } = new();
    }
}
