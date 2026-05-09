using Portivio.Domain.Enums;

namespace Portivio.Application.DTOs.Transaction
{
    public class CreateTransactionRequest
    {
        public Guid InstrumentId { get; set; }
        public TransactionType Type { get; set; }
        public decimal Quantity { get; set; }
        public decimal Price { get; set; }
        public decimal Amount { get; set; }
        public DateTime TransactionDate { get; set; }
        public string Notes { get; set; } = string.Empty;
    }

    public class UpdateTransactionRequest
    {
        public decimal Quantity { get; set; }
        public decimal Price { get; set; }
        public decimal Amount { get; set; }
        public DateTime TransactionDate { get; set; }
        public string Notes { get; set; } = string.Empty;
    }

    public class TransactionResponse
    {
        public Guid Id { get; set; }
        public Guid ProfileId { get; set; }
        public Guid InstrumentId { get; set; }
        public string InstrumentName { get; set; } = string.Empty;
        public string InstrumentSymbol { get; set; } = string.Empty;
        public TransactionType Type { get; set; }
        public decimal Quantity { get; set; }
        public decimal Price { get; set; }
        public decimal Amount { get; set; }
        public DateTime TransactionDate { get; set; }
        public string Notes { get; set; } = string.Empty;
        public bool IsDeleted { get; set; }
        public DateTime CreatedAtUtc { get; set; }
    }
}
