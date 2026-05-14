namespace Portivio.Application.Services
{
    public class EmailSummaryOptions
    {
        public const string SectionName = "EmailSummary";

        public bool DispatcherEnabled { get; set; } = false;
        public string DispatcherCron { get; set; } = "*/5 * * * *";
        public int BatchSize { get; set; } = 100;
        public int ManualCooldownMinutes { get; set; } = 10;
        public int ScheduleLockMinutes { get; set; } = 15;
    }
}
