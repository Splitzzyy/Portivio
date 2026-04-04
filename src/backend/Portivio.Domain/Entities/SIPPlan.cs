namespace Portivio.Domain.Entities
{
    public class SIPPlan
    {
        public Guid Id { get; set; }
        public Guid ProfileId { get; set; }
        public Guid InstrumentId { get; set; }
        public decimal Amount { get; set; }
        public int SIPDay { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public Profile Profile { get; set; } = null!;
        public Instrument Instrument { get; set; } = null!;
    }
}