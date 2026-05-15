using Portivio.Domain.Enums;

namespace Portivio.Application.DTOs.EmailSummary
{
    public class UpdateEmailSummaryPreferenceRequest
    {
        public bool IsEnabled { get; set; }

        public EmailSummaryFrequency? Frequency { get; set; }
        public string? TimeOfDay { get; set; } // HH:mm
        public DayOfWeek? WeeklyDayOfWeek { get; set; }
        public MonthlyDayMode? MonthlyDayMode { get; set; }
        public int? MonthlyDayOfMonth { get; set; }

        public string? TimeZoneId { get; set; }
    }
}
