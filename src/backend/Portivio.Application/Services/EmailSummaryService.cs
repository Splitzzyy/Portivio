using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Portivio.Application.DTOs.EmailSummary;
using Portivio.Application.Results;
using Portivio.Domain.Entities;
using Portivio.Domain.Enums;
using Portivio.Infrastructure.Data;
using System.Globalization;

namespace Portivio.Application.Services
{
    public interface IEmailSummaryService
    {
        Task<Result<EmailSummaryPreferenceResponse>> GetPreferenceAsync(Guid userId);
        Task<Result<EmailSummaryPreferenceResponse>> UpdatePreferenceAsync(Guid userId, UpdateEmailSummaryPreferenceRequest request);

        // Placeholders for future dispatcher integration
        Task<Result> QueueManualSendAsync(Guid userId, CancellationToken cancellationToken = default);
        Task<Result> LockAndQueueDueSendsAsync(CancellationToken cancellationToken = default);
    }

    public class EmailSummaryService : IEmailSummaryService
    {
        private static readonly TimeOnly DefaultTimeOfDay = new(9, 0);
        private const string DefaultTimeZoneId = "UTC";
        private const DayOfWeek DefaultWeeklyDay = DayOfWeek.Monday;
        private const int DefaultMonthlyDayOfMonth = 1;

        private readonly PortivioDbContext _context;
        private readonly ILogger<EmailSummaryService> _logger;

        public EmailSummaryService(PortivioDbContext context, ILogger<EmailSummaryService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<Result<EmailSummaryPreferenceResponse>> GetPreferenceAsync(Guid userId)
        {
            try
            {
                var userExists = await _context.Users.AnyAsync(u => u.Id == userId);
                if (!userExists)
                    return Result<EmailSummaryPreferenceResponse>.NotFound("User not found");

                var pref = await _context.EmailSummaryPreferences.FirstOrDefaultAsync(p => p.UserId == userId);
                if (pref == null)
                {
                    var nowUtc = DateTime.UtcNow;
                    pref = new EmailSummaryPreference
                    {
                        Id = Guid.NewGuid(),
                        UserId = userId,
                        IsEnabled = false,
                        Frequency = EmailSummaryFrequency.Weekly,
                        TimeOfDay = DefaultTimeOfDay,
                        WeeklyDayOfWeek = DefaultWeeklyDay,
                        MonthlyDayMode = MonthlyDayMode.DayOfMonth,
                        MonthlyDayOfMonth = DefaultMonthlyDayOfMonth,
                        TimeZoneId = DefaultTimeZoneId,
                        CreatedAtUtc = nowUtc,
                        UpdatedAtUtc = nowUtc
                    };

                    _context.EmailSummaryPreferences.Add(pref);
                    await _context.SaveChangesAsync();
                }

                return Result<EmailSummaryPreferenceResponse>.Success(ToResponse(pref), "Email summary preference retrieved");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving email summary preference. UserId={UserId}", userId);
                return Result<EmailSummaryPreferenceResponse>.InternalServerError($"Error retrieving preference: {ex.Message}");
            }
        }

        public async Task<Result<EmailSummaryPreferenceResponse>> UpdatePreferenceAsync(Guid userId, UpdateEmailSummaryPreferenceRequest request)
        {
            try
            {
                var userExists = await _context.Users.AnyAsync(u => u.Id == userId);
                if (!userExists)
                    return Result<EmailSummaryPreferenceResponse>.NotFound("User not found");

                var timeZoneId = (request.TimeZoneId ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(timeZoneId))
                    return Result<EmailSummaryPreferenceResponse>.BadRequest("TimeZoneId is required");

                if (!TryGetTimeZone(timeZoneId, out var timeZone))
                    return Result<EmailSummaryPreferenceResponse>.BadRequest("Invalid TimeZoneId");

                var timeOfDay = ParseTimeOfDay(request.TimeOfDay);
                if (request.TimeOfDay != null && timeOfDay == null)
                    return Result<EmailSummaryPreferenceResponse>.BadRequest("TimeOfDay must be in HH:mm format");

                var pref = await _context.EmailSummaryPreferences.FirstOrDefaultAsync(p => p.UserId == userId);
                var nowUtc = DateTime.UtcNow;
                if (pref == null)
                {
                    pref = new EmailSummaryPreference
                    {
                        Id = Guid.NewGuid(),
                        UserId = userId,
                        CreatedAtUtc = nowUtc
                    };
                    _context.EmailSummaryPreferences.Add(pref);
                }

                pref.IsEnabled = request.IsEnabled;
                pref.TimeZoneId = timeZoneId;

                if (request.Frequency.HasValue) pref.Frequency = request.Frequency;
                if (timeOfDay.HasValue) pref.TimeOfDay = timeOfDay;
                if (request.WeeklyDayOfWeek.HasValue) pref.WeeklyDayOfWeek = request.WeeklyDayOfWeek;
                if (request.MonthlyDayMode.HasValue) pref.MonthlyDayMode = request.MonthlyDayMode;
                if (request.MonthlyDayOfMonth.HasValue) pref.MonthlyDayOfMonth = request.MonthlyDayOfMonth;

                if (pref.IsEnabled)
                {
                    var validationResult = ValidateEnabledSchedule(pref);
                    if (validationResult.IsFailure)
                        return Result<EmailSummaryPreferenceResponse>.Failure(validationResult.Message, validationResult.Errors, validationResult.StatusCode ?? 400);

                    pref.NextRunAtUtc = CalculateNextRunAtUtc(pref, nowUtc, timeZone);
                }
                else
                {
                    pref.NextRunAtUtc = null;
                }

                pref.UpdatedAtUtc = nowUtc;
                await _context.SaveChangesAsync();

                return Result<EmailSummaryPreferenceResponse>.Success(ToResponse(pref), "Email summary preference updated");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating email summary preference. UserId={UserId}", userId);
                return Result<EmailSummaryPreferenceResponse>.InternalServerError($"Error updating preference: {ex.Message}");
            }
        }

        public Task<Result> QueueManualSendAsync(Guid userId, CancellationToken cancellationToken = default)
            => Task.FromResult(Result.InternalServerError("Not implemented"));

        public Task<Result> LockAndQueueDueSendsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(Result.InternalServerError("Not implemented"));

        private static EmailSummaryPreferenceResponse ToResponse(EmailSummaryPreference pref)
        {
            return new EmailSummaryPreferenceResponse
            {
                Id = pref.Id,
                UserId = pref.UserId,
                IsEnabled = pref.IsEnabled,
                Frequency = pref.Frequency,
                TimeOfDay = pref.TimeOfDay?.ToString("HH:mm", CultureInfo.InvariantCulture),
                WeeklyDayOfWeek = pref.WeeklyDayOfWeek,
                MonthlyDayMode = pref.MonthlyDayMode,
                MonthlyDayOfMonth = pref.MonthlyDayOfMonth,
                TimeZoneId = pref.TimeZoneId,
                NextRunAtUtc = pref.NextRunAtUtc,
                CreatedAtUtc = pref.CreatedAtUtc,
                UpdatedAtUtc = pref.UpdatedAtUtc
            };
        }

        private static Result ValidateEnabledSchedule(EmailSummaryPreference pref)
        {
            if (!pref.Frequency.HasValue)
                return Result.BadRequest("Frequency is required when enabled");
            if (!pref.TimeOfDay.HasValue)
                return Result.BadRequest("TimeOfDay is required when enabled");
            if (string.IsNullOrWhiteSpace(pref.TimeZoneId))
                return Result.BadRequest("TimeZoneId is required when enabled");

            switch (pref.Frequency.Value)
            {
                case EmailSummaryFrequency.Daily:
                    return Result.Success();
                case EmailSummaryFrequency.Weekly:
                    if (!pref.WeeklyDayOfWeek.HasValue)
                        return Result.BadRequest("WeeklyDayOfWeek is required for weekly frequency");
                    return Result.Success();
                case EmailSummaryFrequency.Monthly:
                    if (!pref.MonthlyDayMode.HasValue)
                        return Result.BadRequest("MonthlyDayMode is required for monthly frequency");
                    if (pref.MonthlyDayMode.Value == MonthlyDayMode.DayOfMonth)
                    {
                        if (!pref.MonthlyDayOfMonth.HasValue)
                            return Result.BadRequest("MonthlyDayOfMonth is required for monthly day-of-month mode");
                        if (pref.MonthlyDayOfMonth.Value < 1 || pref.MonthlyDayOfMonth.Value > 31)
                            return Result.BadRequest("MonthlyDayOfMonth must be between 1 and 31");
                    }
                    return Result.Success();
                default:
                    return Result.BadRequest("Unsupported frequency");
            }
        }

        private static bool TryGetTimeZone(string timeZoneId, out TimeZoneInfo timeZone)
        {
            try
            {
                timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
                return true;
            }
            catch (TimeZoneNotFoundException)
            {
                timeZone = TimeZoneInfo.Utc;
                return false;
            }
            catch (InvalidTimeZoneException)
            {
                timeZone = TimeZoneInfo.Utc;
                return false;
            }
        }

        private static TimeOnly? ParseTimeOfDay(string? timeOfDay)
        {
            if (string.IsNullOrWhiteSpace(timeOfDay))
                return null;

            return TimeOnly.TryParseExact(timeOfDay.Trim(), "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
                ? parsed
                : null;
        }

        private static DateTime CalculateNextRunAtUtc(EmailSummaryPreference pref, DateTime nowUtc, TimeZoneInfo timeZone)
        {
            var nowLocal = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, timeZone);
            var candidateLocal = new DateTime(nowLocal.Year, nowLocal.Month, nowLocal.Day, 0, 0, 0, DateTimeKind.Unspecified)
                .Add(pref.TimeOfDay!.Value.ToTimeSpan());

            DateTime nextLocal = pref.Frequency!.Value switch
            {
                EmailSummaryFrequency.Daily => candidateLocal <= nowLocal ? candidateLocal.AddDays(1) : candidateLocal,
                EmailSummaryFrequency.Weekly => NextWeekly(pref, nowLocal, candidateLocal),
                EmailSummaryFrequency.Monthly => NextMonthly(pref, nowLocal, candidateLocal),
                _ => candidateLocal.AddDays(1)
            };

            return TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(nextLocal, DateTimeKind.Unspecified), timeZone);
        }

        private static DateTime NextWeekly(EmailSummaryPreference pref, DateTime nowLocal, DateTime todayAtTimeLocal)
        {
            var target = pref.WeeklyDayOfWeek!.Value;
            var today = nowLocal.DayOfWeek;
            var daysAhead = ((int)target - (int)today + 7) % 7;

            if (daysAhead == 0 && todayAtTimeLocal <= nowLocal)
                daysAhead = 7;

            return todayAtTimeLocal.AddDays(daysAhead);
        }

        private static DateTime NextMonthly(EmailSummaryPreference pref, DateTime nowLocal, DateTime todayAtTimeLocal)
        {
            var mode = pref.MonthlyDayMode!.Value;
            var year = nowLocal.Year;
            var month = nowLocal.Month;

            DateTime candidate = mode switch
            {
                MonthlyDayMode.LastDay => LastDayOfMonthAtTime(year, month, pref.TimeOfDay!.Value),
                MonthlyDayMode.DayOfMonth => DayOfMonthAtTime(year, month, pref.MonthlyDayOfMonth!.Value, pref.TimeOfDay!.Value),
                _ => DayOfMonthAtTime(year, month, DefaultMonthlyDayOfMonth, pref.TimeOfDay!.Value)
            };

            if (candidate <= nowLocal)
            {
                var nextMonth = nowLocal.AddMonths(1);
                year = nextMonth.Year;
                month = nextMonth.Month;
                candidate = mode switch
                {
                    MonthlyDayMode.LastDay => LastDayOfMonthAtTime(year, month, pref.TimeOfDay!.Value),
                    MonthlyDayMode.DayOfMonth => DayOfMonthAtTime(year, month, pref.MonthlyDayOfMonth!.Value, pref.TimeOfDay!.Value),
                    _ => DayOfMonthAtTime(year, month, DefaultMonthlyDayOfMonth, pref.TimeOfDay!.Value)
                };
            }

            return candidate;
        }

        private static DateTime LastDayOfMonthAtTime(int year, int month, TimeOnly timeOfDay)
        {
            var lastDay = DateTime.DaysInMonth(year, month);
            return new DateTime(year, month, lastDay, 0, 0, 0, DateTimeKind.Unspecified)
                .Add(timeOfDay.ToTimeSpan());
        }

        private static DateTime DayOfMonthAtTime(int year, int month, int dayOfMonth, TimeOnly timeOfDay)
        {
            var daysInMonth = DateTime.DaysInMonth(year, month);
            var day = Math.Min(Math.Max(1, dayOfMonth), daysInMonth);
            return new DateTime(year, month, day, 0, 0, 0, DateTimeKind.Unspecified)
                .Add(timeOfDay.ToTimeSpan());
        }
    }
}

