namespace Portivio.Application.DTOs.SIPPlan
{
    public class CreateSIPPlanRequest
    {
        public Guid InstrumentId { get; set; }
        public decimal Amount { get; set; }
        public int SIPDay { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
    }

    public class UpdateSIPPlanRequest
    {
        public decimal Amount { get; set; }
        public int SIPDay { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
    }

    public class SIPPlanResponse
    {
        public Guid Id { get; set; }
        public Guid ProfileId { get; set; }
        public Guid InstrumentId { get; set; }
        public string InstrumentName { get; set; } = string.Empty;
        public string InstrumentSymbol { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public int SIPDay { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
