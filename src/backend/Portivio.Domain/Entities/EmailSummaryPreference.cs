using Portivio.Domain.Enums;

namespace Portivio.Domain.Entities
{
    public class EmailSummaryPreference
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }

        public bool IsEnabled { get; set; }

        public EmailSummaryFrequency? Frequency { get; set; }
        public TimeOnly? TimeOfDay { get; set; }
        public DayOfWeek? WeeklyDayOfWeek { get; set; }
        public MonthlyDayMode? MonthlyDayMode { get; set; }
        public int? MonthlyDayOfMonth { get; set; }

        public string TimeZoneId { get; set; } = null!;

        public EmailSummarySendStatus? LastSendStatus { get; set; }
        public DateTime? LastSendAttemptAtUtc { get; set; }
        public DateTime? LastSendSucceededAtUtc { get; set; }
        public string? LastSendError { get; set; }

        public DateTime? LastManualQueuedAtUtc { get; set; }
        public DateTime? NextRunAtUtc { get; set; }
        public DateTime? LockedUntilUtc { get; set; }

        public DateTime CreatedAtUtc { get; set; }
        public DateTime UpdatedAtUtc { get; set; }

        public User User { get; set; } = null!;
    }
}
