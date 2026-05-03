using Portivio.Domain.Enums;

namespace Portivio.Domain.Entities
{
    public class Transaction
    {
        public Guid Id { get; set; }

        public Guid ProfileId { get; set; }

        public Guid InstrumentId { get; set; }

        public TransactionType Type { get; set; }

        public decimal Quantity { get; set; }

        public decimal Price { get; set; }

        public decimal Amount { get; set; }

        public DateTime TransactionDate { get; set; }

        public string Notes { get; set; } = null!;

        public string? ClientTxnId { get; set; }

        public TransactionSource Source { get; set; }

        public bool IsDeleted { get; set; }

        public DateTime CreatedAtUtc { get; set; }

        public DateTime UpdatedAtUtc { get; set; }

        public Profile Profile { get; set; } = null!;

        public Instrument Instrument { get; set; } = null!;
    }
}
