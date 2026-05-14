namespace Portivio.Application.Services
{
    public class EmailSummaryOptions
    {
        public const string SectionName = "EmailSummary";

        public int ManualQueueCooldownMinutes { get; set; } = 15;
        public int MaxLockMinutes { get; set; } = 15;
    }
}
