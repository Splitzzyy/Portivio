using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Portivio.Application.DTOs.EmailSummary;
using Portivio.Application.Services;
using Portivio.Domain.Entities;
using Portivio.Domain.Enums;
using Portivio.Infrastructure.Data;
using Portivio.Infrastructure.Services;
using Xunit;

namespace Portivio.Tests.Services
{
    public class EmailSummaryServiceTests
    {
        private PortivioDbContext CreateInMemoryDbContext()
        {
            var options = new DbContextOptionsBuilder<PortivioDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
            return new PortivioDbContext(options);
        }

        private static ILogger<EmailSummaryService> CreateMockLogger() => new Mock<ILogger<EmailSummaryService>>().Object;

        private static IOptions<EmailSummaryOptions> CreateEmailSummaryOptions(
            int cooldownMinutes = 10,
            int batchSize = 100,
            int scheduleLockMinutes = 15,
            bool dispatcherEnabled = false,
            string? dispatcherCron = null)
            => Options.Create(new EmailSummaryOptions
            {
                ManualCooldownMinutes = cooldownMinutes,
                BatchSize = batchSize,
                ScheduleLockMinutes = scheduleLockMinutes,
                DispatcherEnabled = dispatcherEnabled,
                DispatcherCron = dispatcherCron ?? "*/5 * * * *"
            });

        private static IOptions<EmailOptions> CreateEmailOptions(string frontendBaseUrl = "https://app.portivio.app")
            => Options.Create(new EmailOptions { FrontendBaseUrl = frontendBaseUrl });

        private static User CreateUser(Guid? id = null) => new()
        {
            Id = id ?? Guid.NewGuid(),
            Email = $"user-{Guid.NewGuid()}@example.com",
            Name = "Test User",
            PasswordHash = "hash",
            IsVerified = true,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        private static EmailSummaryPreference CreateScheduledPreference(
            Guid userId,
            DateTime nextRunAtUtc,
            bool isEnabled = true,
            EmailSummaryFrequency frequency = EmailSummaryFrequency.Daily,
            string timeZoneId = "UTC")
            => new()
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                IsEnabled = isEnabled,
                Frequency = frequency,
                TimeOfDay = new TimeOnly(9, 0),
                WeeklyDayOfWeek = DayOfWeek.Monday,
                MonthlyDayMode = MonthlyDayMode.DayOfMonth,
                MonthlyDayOfMonth = 1,
                TimeZoneId = timeZoneId,
                NextRunAtUtc = nextRunAtUtc,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            };

        private static TimeZoneInfo FindTimeZone(string primaryId, string fallbackId = "UTC")
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(primaryId);
            }
            catch (TimeZoneNotFoundException)
            {
                return TimeZoneInfo.FindSystemTimeZoneById(fallbackId);
            }
        }

        private static EmailSummaryService CreateService(
            PortivioDbContext context,
            Mock<IEmailService>? emailServiceMock = null,
            Mock<Hangfire.IBackgroundJobClient>? backgroundJobClientMock = null,
            IOptions<EmailSummaryOptions>? summaryOptions = null,
            IOptions<EmailOptions>? emailOptions = null)
        {
            emailServiceMock ??= new Mock<IEmailService>();
            backgroundJobClientMock ??= new Mock<Hangfire.IBackgroundJobClient>();
            summaryOptions ??= CreateEmailSummaryOptions();
            emailOptions ??= CreateEmailOptions();

            return new EmailSummaryService(
                context,
                emailServiceMock.Object,
                backgroundJobClientMock.Object,
                summaryOptions,
                emailOptions,
                CreateMockLogger());
        }

        [Fact]
        public async Task GetPreference_WhenMissing_ReturnsDefaultAndPersists()
        {
            using var context = CreateInMemoryDbContext();
            var user = CreateUser();
            context.Users.Add(user);
            await context.SaveChangesAsync();

            var service = CreateService(context);
            var result = await service.GetPreferenceAsync(user.Id);

            Assert.True(result.IsSuccess);
            Assert.NotNull(result.Data);
            Assert.False(result.Data!.IsEnabled);
            Assert.Equal("UTC", result.Data.TimeZoneId);
            Assert.Null(result.Data.NextRunAtUtc);

            var persisted = await context.EmailSummaryPreferences.FirstOrDefaultAsync(p => p.UserId == user.Id);
            Assert.NotNull(persisted);
        }

        [Fact]
        public async Task UpdatePreference_Daily_Valid_ReturnsNextRun()
        {
            using var context = CreateInMemoryDbContext();
            var user = CreateUser();
            context.Users.Add(user);
            await context.SaveChangesAsync();

            var service = CreateService(context);
            var startUtc = DateTime.UtcNow;
            var result = await service.UpdatePreferenceAsync(user.Id, new UpdateEmailSummaryPreferenceRequest
            {
                IsEnabled = true,
                TimeZoneId = "UTC",
                Frequency = EmailSummaryFrequency.Daily,
                TimeOfDay = "09:00"
            });

            Assert.True(result.IsSuccess);
            Assert.NotNull(result.Data);
            Assert.True(result.Data!.IsEnabled);
            Assert.NotNull(result.Data.NextRunAtUtc);
            Assert.True(result.Data.NextRunAtUtc >= startUtc);
        }

        [Fact]
        public async Task UpdatePreference_Weekly_Valid_ReturnsNextRun()
        {
            using var context = CreateInMemoryDbContext();
            var user = CreateUser();
            context.Users.Add(user);
            await context.SaveChangesAsync();

            var service = CreateService(context);
            var result = await service.UpdatePreferenceAsync(user.Id, new UpdateEmailSummaryPreferenceRequest
            {
                IsEnabled = true,
                TimeZoneId = "UTC",
                Frequency = EmailSummaryFrequency.Weekly,
                TimeOfDay = "09:00",
                WeeklyDayOfWeek = DayOfWeek.Monday
            });

            Assert.True(result.IsSuccess);
            Assert.NotNull(result.Data);
            Assert.NotNull(result.Data!.NextRunAtUtc);
        }

        [Fact]
        public async Task UpdatePreference_Monthly_DayOfMonth_Valid_ReturnsNextRun()
        {
            using var context = CreateInMemoryDbContext();
            var user = CreateUser();
            context.Users.Add(user);
            await context.SaveChangesAsync();

            var service = CreateService(context);
            var result = await service.UpdatePreferenceAsync(user.Id, new UpdateEmailSummaryPreferenceRequest
            {
                IsEnabled = true,
                TimeZoneId = "UTC",
                Frequency = EmailSummaryFrequency.Monthly,
                TimeOfDay = "09:00",
                MonthlyDayMode = MonthlyDayMode.DayOfMonth,
                MonthlyDayOfMonth = 15
            });

            Assert.True(result.IsSuccess);
            Assert.NotNull(result.Data);
            Assert.NotNull(result.Data!.NextRunAtUtc);
        }

        [Fact]
        public async Task UpdatePreference_Monthly_LastDay_Valid_ReturnsNextRun()
        {
            using var context = CreateInMemoryDbContext();
            var user = CreateUser();
            context.Users.Add(user);
            await context.SaveChangesAsync();

            var service = CreateService(context);
            var result = await service.UpdatePreferenceAsync(user.Id, new UpdateEmailSummaryPreferenceRequest
            {
                IsEnabled = true,
                TimeZoneId = "UTC",
                Frequency = EmailSummaryFrequency.Monthly,
                TimeOfDay = "09:00",
                MonthlyDayMode = MonthlyDayMode.LastDay
            });

            Assert.True(result.IsSuccess);
            Assert.NotNull(result.Data);
            Assert.NotNull(result.Data!.NextRunAtUtc);
        }

        [Fact]
        public async Task UpdatePreference_Disabled_IncompleteRequest_PreservesScheduleAndClearsNextRun()
        {
            using var context = CreateInMemoryDbContext();
            var user = CreateUser();
            context.Users.Add(user);
            await context.SaveChangesAsync();

            var service = CreateService(context);
            var enabled = await service.UpdatePreferenceAsync(user.Id, new UpdateEmailSummaryPreferenceRequest
            {
                IsEnabled = true,
                TimeZoneId = "UTC",
                Frequency = EmailSummaryFrequency.Daily,
                TimeOfDay = "09:00"
            });
            Assert.True(enabled.IsSuccess);

            var disabled = await service.UpdatePreferenceAsync(user.Id, new UpdateEmailSummaryPreferenceRequest
            {
                IsEnabled = false
            });

            Assert.True(disabled.IsSuccess);
            Assert.NotNull(disabled.Data);
            Assert.False(disabled.Data!.IsEnabled);
            Assert.Null(disabled.Data.NextRunAtUtc);
            Assert.Equal(EmailSummaryFrequency.Daily, disabled.Data.Frequency);
            Assert.Equal("09:00", disabled.Data.TimeOfDay);
            Assert.Equal("UTC", disabled.Data.TimeZoneId);
        }

        [Fact]
        public async Task UpdatePreference_EnabledWithoutFrequency_ReturnsBadRequestAndDoesNotPersist()
        {
            using var context = CreateInMemoryDbContext();
            var user = CreateUser();
            context.Users.Add(user);
            await context.SaveChangesAsync();

            var service = CreateService(context);
            var result = await service.UpdatePreferenceAsync(user.Id, new UpdateEmailSummaryPreferenceRequest
            {
                IsEnabled = true,
                TimeZoneId = "UTC",
                TimeOfDay = "09:00"
            });

            Assert.False(result.IsSuccess);
            Assert.Equal(400, result.StatusCode);

            context.ChangeTracker.Clear();
            var persisted = await context.EmailSummaryPreferences.FirstOrDefaultAsync(p => p.UserId == user.Id);
            Assert.Null(persisted);
        }

        [Fact]
        public async Task UpdatePreference_InvalidMonthlyDay_ReturnsBadRequest()
        {
            using var context = CreateInMemoryDbContext();
            var user = CreateUser();
            context.Users.Add(user);
            await context.SaveChangesAsync();

            var service = CreateService(context);
            var result = await service.UpdatePreferenceAsync(user.Id, new UpdateEmailSummaryPreferenceRequest
            {
                IsEnabled = true,
                TimeZoneId = "UTC",
                Frequency = EmailSummaryFrequency.Monthly,
                TimeOfDay = "09:00",
                MonthlyDayMode = MonthlyDayMode.DayOfMonth,
                MonthlyDayOfMonth = 0
            });

            Assert.False(result.IsSuccess);
            Assert.Equal(400, result.StatusCode);
        }

        [Fact]
        public async Task QueueManualSummary_WhenMissing_CreatesPreference_Disabled_AndQueues()
        {
            using var context = CreateInMemoryDbContext();
            var user = CreateUser();
            context.Users.Add(user);
            await context.SaveChangesAsync();

            var emailServiceMock = new Mock<IEmailService>();
            var backgroundJobClientMock = new Mock<Hangfire.IBackgroundJobClient>();
            backgroundJobClientMock
                .Setup(c => c.Create(It.IsAny<Hangfire.Common.Job>(), It.IsAny<Hangfire.States.IState>()))
                .Returns("job-id");

            var service = CreateService(context, emailServiceMock, backgroundJobClientMock);
            var result = await service.QueueManualSummaryAsync(user.Id);

            Assert.True(result.IsSuccess);
            Assert.NotNull(result.Data);
            Assert.False(result.Data!.IsEnabled);
            Assert.Equal(EmailSummarySendStatus.Queued, result.Data.LastSendStatus);
            Assert.NotNull(result.Data.LastManualQueuedAtUtc);

            var persisted = await context.EmailSummaryPreferences.FirstOrDefaultAsync(p => p.UserId == user.Id);
            Assert.NotNull(persisted);
            Assert.False(persisted!.IsEnabled);
            Assert.Equal(EmailSummarySendStatus.Queued, persisted.LastSendStatus);
            Assert.NotNull(persisted.LastManualQueuedAtUtc);

            backgroundJobClientMock.Verify(
                c => c.Create(It.IsAny<Hangfire.Common.Job>(), It.IsAny<Hangfire.States.IState>()),
                Times.Once);
        }

        [Fact]
        public async Task QueueManualSummary_RejectsInactiveOrUnverifiedUsers()
        {
            using var context = CreateInMemoryDbContext();
            var inactiveUser = CreateUser();
            inactiveUser.IsActive = false;
            var unverifiedUser = CreateUser();
            unverifiedUser.IsVerified = false;
            context.Users.AddRange(inactiveUser, unverifiedUser);
            await context.SaveChangesAsync();

            var service = CreateService(context);

            var inactiveResult = await service.QueueManualSummaryAsync(inactiveUser.Id);
            Assert.False(inactiveResult.IsSuccess);
            Assert.Equal(403, inactiveResult.StatusCode);

            var unverifiedResult = await service.QueueManualSummaryAsync(unverifiedUser.Id);
            Assert.False(unverifiedResult.IsSuccess);
            Assert.Equal(403, unverifiedResult.StatusCode);
        }

        [Fact]
        public async Task QueueManualSummary_EnforcesCooldown_AfterSuccessfulQueue()
        {
            using var context = CreateInMemoryDbContext();
            var user = CreateUser();
            context.Users.Add(user);
            var pref = new EmailSummaryPreference
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                IsEnabled = false,
                Frequency = EmailSummaryFrequency.Weekly,
                TimeOfDay = new TimeOnly(9, 0),
                WeeklyDayOfWeek = DayOfWeek.Monday,
                MonthlyDayMode = MonthlyDayMode.DayOfMonth,
                MonthlyDayOfMonth = 1,
                TimeZoneId = "UTC",
                LastManualQueuedAtUtc = DateTime.UtcNow.AddMinutes(-5),
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            };
            context.EmailSummaryPreferences.Add(pref);
            await context.SaveChangesAsync();

            var backgroundJobClientMock = new Mock<Hangfire.IBackgroundJobClient>();
            var service = CreateService(context, backgroundJobClientMock: backgroundJobClientMock);

            var result = await service.QueueManualSummaryAsync(user.Id);
            Assert.False(result.IsSuccess);
            Assert.Equal(409, result.StatusCode);
        }

        [Fact]
        public async Task SendQueuedSummary_AllowsEmptyAccount_AndUpdatesStatus()
        {
            using var context = CreateInMemoryDbContext();
            var user = CreateUser();
            context.Users.Add(user);
            var pref = new EmailSummaryPreference
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                IsEnabled = false,
                Frequency = EmailSummaryFrequency.Weekly,
                TimeOfDay = new TimeOnly(9, 0),
                WeeklyDayOfWeek = DayOfWeek.Monday,
                MonthlyDayMode = MonthlyDayMode.DayOfMonth,
                MonthlyDayOfMonth = 1,
                TimeZoneId = "UTC",
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            };
            context.EmailSummaryPreferences.Add(pref);
            await context.SaveChangesAsync();

            var emailServiceMock = new Mock<IEmailService>();
            var service = CreateService(context, emailServiceMock: emailServiceMock);

            await service.SendQueuedSummaryAsync(pref.Id, isManual: true);

            var updated = await context.EmailSummaryPreferences.FirstAsync(p => p.Id == pref.Id);
            Assert.Equal(EmailSummarySendStatus.Succeeded, updated.LastSendStatus);
            Assert.NotNull(updated.LastSendAttemptAtUtc);
            Assert.NotNull(updated.LastSendSucceededAtUtc);
            Assert.Null(updated.LastSendError);

            emailServiceMock.Verify(
                s => s.SendInvestmentSummaryAsync(It.IsAny<InvestmentSummaryEmailModel>(), It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task SendQueuedSummary_WhenSendFails_PersistsFailedStatusAndError()
        {
            using var context = CreateInMemoryDbContext();
            var user = CreateUser();
            context.Users.Add(user);
            var pref = new EmailSummaryPreference
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                IsEnabled = false,
                Frequency = EmailSummaryFrequency.Weekly,
                TimeOfDay = new TimeOnly(9, 0),
                WeeklyDayOfWeek = DayOfWeek.Monday,
                MonthlyDayMode = MonthlyDayMode.DayOfMonth,
                MonthlyDayOfMonth = 1,
                TimeZoneId = "UTC",
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            };
            context.EmailSummaryPreferences.Add(pref);
            await context.SaveChangesAsync();

            var emailServiceMock = new Mock<IEmailService>();
            emailServiceMock
                .Setup(s => s.SendInvestmentSummaryAsync(It.IsAny<InvestmentSummaryEmailModel>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("smtp down"));

            var service = CreateService(context, emailServiceMock: emailServiceMock);

            await Assert.ThrowsAsync<InvalidOperationException>(() => service.SendQueuedSummaryAsync(pref.Id, isManual: true));

            var updated = await context.EmailSummaryPreferences.FirstAsync(p => p.Id == pref.Id);
            Assert.Equal(EmailSummarySendStatus.Failed, updated.LastSendStatus);
            Assert.NotNull(updated.LastSendAttemptAtUtc);
            Assert.Contains("smtp down", updated.LastSendError);
        }

        [Fact]
        public async Task LockAndQueueDueSendsAsync_ClaimsUnlockedDueBatch_AndQueuesOneJobPerPreference()
        {
            using var context = CreateInMemoryDbContext();
            var dueAt = DateTime.UtcNow.AddMinutes(-10);
            var userA = CreateUser();
            var userB = CreateUser();
            var userC = CreateUser();
            context.Users.AddRange(userA, userB, userC);

            var prefA = CreateScheduledPreference(userA.Id, dueAt.AddMinutes(-2));
            var prefB = CreateScheduledPreference(userB.Id, dueAt.AddMinutes(-1));
            var prefC = CreateScheduledPreference(userC.Id, dueAt);
            context.EmailSummaryPreferences.AddRange(prefA, prefB, prefC);
            await context.SaveChangesAsync();

            var backgroundJobClientMock = new Mock<Hangfire.IBackgroundJobClient>();
            backgroundJobClientMock
                .Setup(c => c.Create(It.IsAny<Hangfire.Common.Job>(), It.IsAny<Hangfire.States.IState>()))
                .Returns("job-id");

            var service = CreateService(
                context,
                backgroundJobClientMock: backgroundJobClientMock,
                summaryOptions: CreateEmailSummaryOptions(batchSize: 2, scheduleLockMinutes: 20));

            var result = await service.LockAndQueueDueSendsAsync();

            Assert.True(result.IsSuccess);

            context.ChangeTracker.Clear();
            var storedA = await context.EmailSummaryPreferences.FirstAsync(p => p.Id == prefA.Id);
            var storedB = await context.EmailSummaryPreferences.FirstAsync(p => p.Id == prefB.Id);
            var storedC = await context.EmailSummaryPreferences.FirstAsync(p => p.Id == prefC.Id);

            Assert.Equal(EmailSummarySendStatus.Queued, storedA.LastSendStatus);
            Assert.Equal(EmailSummarySendStatus.Queued, storedB.LastSendStatus);
            Assert.Null(storedA.LastSendError);
            Assert.Null(storedB.LastSendError);
            Assert.NotNull(storedA.LockedUntilUtc);
            Assert.NotNull(storedB.LockedUntilUtc);
            Assert.True(storedA.NextRunAtUtc > dueAt);
            Assert.True(storedB.NextRunAtUtc > dueAt);
            Assert.Equal(prefC.NextRunAtUtc, storedC.NextRunAtUtc);
            Assert.Null(storedC.LockedUntilUtc);

            backgroundJobClientMock.Verify(
                c => c.Create(It.IsAny<Hangfire.Common.Job>(), It.IsAny<Hangfire.States.IState>()),
                Times.Exactly(2));
        }

        [Fact]
        public async Task LockAndQueueDueSendsAsync_SkipsLockedAndFuturePreferences()
        {
            using var context = CreateInMemoryDbContext();
            var userA = CreateUser();
            var userB = CreateUser();
            var userC = CreateUser();
            context.Users.AddRange(userA, userB, userC);

            var dueUnlocked = CreateScheduledPreference(userA.Id, DateTime.UtcNow.AddMinutes(-5));
            var dueLocked = CreateScheduledPreference(userB.Id, DateTime.UtcNow.AddMinutes(-5));
            dueLocked.LockedUntilUtc = DateTime.UtcNow.AddMinutes(5);
            var future = CreateScheduledPreference(userC.Id, DateTime.UtcNow.AddMinutes(5));

            context.EmailSummaryPreferences.AddRange(dueUnlocked, dueLocked, future);
            await context.SaveChangesAsync();

            var backgroundJobClientMock = new Mock<Hangfire.IBackgroundJobClient>();
            backgroundJobClientMock
                .Setup(c => c.Create(It.IsAny<Hangfire.Common.Job>(), It.IsAny<Hangfire.States.IState>()))
                .Returns("job-id");

            var service = CreateService(context, backgroundJobClientMock: backgroundJobClientMock);

            var result = await service.LockAndQueueDueSendsAsync();

            Assert.True(result.IsSuccess);
            context.ChangeTracker.Clear();

            var storedUnlocked = await context.EmailSummaryPreferences.FirstAsync(p => p.Id == dueUnlocked.Id);
            var storedLocked = await context.EmailSummaryPreferences.FirstAsync(p => p.Id == dueLocked.Id);
            var storedFuture = await context.EmailSummaryPreferences.FirstAsync(p => p.Id == future.Id);

            Assert.Equal(EmailSummarySendStatus.Queued, storedUnlocked.LastSendStatus);
            Assert.Equal(dueLocked.LockedUntilUtc, storedLocked.LockedUntilUtc);
            Assert.Null(storedLocked.LastSendStatus);
            Assert.Null(storedFuture.LastSendStatus);

            backgroundJobClientMock.Verify(
                c => c.Create(It.IsAny<Hangfire.Common.Job>(), It.IsAny<Hangfire.States.IState>()),
                Times.Once);
        }

        [Fact]
        public async Task SendQueuedSummary_Scheduled_WhenUserInactive_SkipsAndClearsLock()
        {
            using var context = CreateInMemoryDbContext();
            var user = CreateUser();
            user.IsActive = false;
            context.Users.Add(user);
            var pref = CreateScheduledPreference(user.Id, DateTime.UtcNow.AddMinutes(-5));
            pref.LockedUntilUtc = DateTime.UtcNow.AddMinutes(5);
            context.EmailSummaryPreferences.Add(pref);
            await context.SaveChangesAsync();

            var emailServiceMock = new Mock<IEmailService>();
            var service = CreateService(context, emailServiceMock: emailServiceMock);

            await service.SendQueuedSummaryAsync(pref.Id, isManual: false);

            var updated = await context.EmailSummaryPreferences.FirstAsync(p => p.Id == pref.Id);
            Assert.Equal(EmailSummarySendStatus.Skipped, updated.LastSendStatus);
            Assert.Equal("User inactive or unverified", updated.LastSendError);
            Assert.Null(updated.LockedUntilUtc);

            emailServiceMock.Verify(
                s => s.SendInvestmentSummaryAsync(It.IsAny<InvestmentSummaryEmailModel>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }

        [Fact]
        public async Task SendQueuedSummary_Scheduled_WhenAccountEmpty_SkipsAndClearsLock()
        {
            using var context = CreateInMemoryDbContext();
            var user = CreateUser();
            context.Users.Add(user);
            var pref = CreateScheduledPreference(user.Id, DateTime.UtcNow.AddMinutes(-5));
            pref.LockedUntilUtc = DateTime.UtcNow.AddMinutes(5);
            context.EmailSummaryPreferences.Add(pref);
            await context.SaveChangesAsync();

            var emailServiceMock = new Mock<IEmailService>();
            var service = CreateService(context, emailServiceMock: emailServiceMock);

            await service.SendQueuedSummaryAsync(pref.Id, isManual: false);

            var updated = await context.EmailSummaryPreferences.FirstAsync(p => p.Id == pref.Id);
            Assert.Equal(EmailSummarySendStatus.Skipped, updated.LastSendStatus);
            Assert.Equal("No scheduled summary content available", updated.LastSendError);
            Assert.Null(updated.LockedUntilUtc);

            emailServiceMock.Verify(
                s => s.SendInvestmentSummaryAsync(It.IsAny<InvestmentSummaryEmailModel>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }

        [Fact]
        public async Task SendQueuedSummary_Scheduled_WhenSendSucceeds_SetsSucceededAndClearsLock()
        {
            using var context = CreateInMemoryDbContext();
            var user = CreateUser();
            context.Users.Add(user);
            var profile = new Profile
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                Name = "Main",
                BaseCurrency = "INR",
                Description = "Primary",
                CreatedAt = DateTime.UtcNow
            };
            var holding = new Holding
            {
                Id = Guid.NewGuid(),
                ProfileId = profile.Id,
                InstrumentId = Guid.NewGuid(),
                Quantity = 1,
                AvgPrice = 100,
                CurrentPrice = 120,
                MarketValue = 120,
                UnrealizedPnL = 20,
                LastUpdated = DateTime.UtcNow
            };
            context.Profiles.Add(profile);
            context.Holdings.Add(holding);

            var pref = CreateScheduledPreference(user.Id, DateTime.UtcNow.AddMinutes(-5));
            pref.LockedUntilUtc = DateTime.UtcNow.AddMinutes(5);
            context.EmailSummaryPreferences.Add(pref);
            await context.SaveChangesAsync();

            var emailServiceMock = new Mock<IEmailService>();
            var service = CreateService(context, emailServiceMock: emailServiceMock);

            await service.SendQueuedSummaryAsync(pref.Id, isManual: false);

            var updated = await context.EmailSummaryPreferences.FirstAsync(p => p.Id == pref.Id);
            Assert.Equal(EmailSummarySendStatus.Succeeded, updated.LastSendStatus);
            Assert.NotNull(updated.LastSendSucceededAtUtc);
            Assert.Null(updated.LastSendError);
            Assert.Null(updated.LockedUntilUtc);

            emailServiceMock.Verify(
                s => s.SendInvestmentSummaryAsync(It.Is<InvestmentSummaryEmailModel>(m => !m.IsEmptyAccount), It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public void CalculateNextRunAtUtc_Daily_UsesLocalTime()
        {
            var pref = CreateScheduledPreference(Guid.NewGuid(), DateTime.UtcNow);
            pref.Frequency = EmailSummaryFrequency.Daily;
            pref.TimeOfDay = new TimeOnly(9, 0);
            pref.TimeZoneId = "UTC";

            var nextRun = EmailSummaryService.CalculateNextRunAtUtc(
                pref,
                new DateTime(2026, 5, 15, 10, 0, 0, DateTimeKind.Utc),
                TimeZoneInfo.Utc);

            Assert.Equal(new DateTime(2026, 5, 16, 9, 0, 0, DateTimeKind.Utc), nextRun);
        }

        [Fact]
        public void CalculateNextRunAtUtc_Weekly_UsesTargetWeekday()
        {
            var pref = CreateScheduledPreference(Guid.NewGuid(), DateTime.UtcNow, frequency: EmailSummaryFrequency.Weekly);
            pref.TimeOfDay = new TimeOnly(9, 0);
            pref.WeeklyDayOfWeek = DayOfWeek.Monday;
            pref.TimeZoneId = "UTC";

            var nextRun = EmailSummaryService.CalculateNextRunAtUtc(
                pref,
                new DateTime(2026, 5, 15, 10, 0, 0, DateTimeKind.Utc),
                TimeZoneInfo.Utc);

            Assert.Equal(new DateTime(2026, 5, 18, 9, 0, 0, DateTimeKind.Utc), nextRun);
        }

        [Fact]
        public void CalculateNextRunAtUtc_MonthlyDayOfMonth_ClampsMissingDays()
        {
            var pref = CreateScheduledPreference(Guid.NewGuid(), DateTime.UtcNow, frequency: EmailSummaryFrequency.Monthly);
            pref.TimeOfDay = new TimeOnly(9, 0);
            pref.MonthlyDayMode = MonthlyDayMode.DayOfMonth;
            pref.MonthlyDayOfMonth = 31;
            pref.TimeZoneId = "UTC";

            var nextRun = EmailSummaryService.CalculateNextRunAtUtc(
                pref,
                new DateTime(2026, 4, 29, 10, 0, 0, DateTimeKind.Utc),
                TimeZoneInfo.Utc);

            Assert.Equal(new DateTime(2026, 4, 30, 9, 0, 0, DateTimeKind.Utc), nextRun);
        }

        [Fact]
        public void CalculateNextRunAtUtc_MonthlyLastDay_UsesMonthEnd()
        {
            var pref = CreateScheduledPreference(Guid.NewGuid(), DateTime.UtcNow, frequency: EmailSummaryFrequency.Monthly);
            pref.TimeOfDay = new TimeOnly(9, 0);
            pref.MonthlyDayMode = MonthlyDayMode.LastDay;
            pref.TimeZoneId = "UTC";

            var nextRun = EmailSummaryService.CalculateNextRunAtUtc(
                pref,
                new DateTime(2026, 4, 29, 10, 0, 0, DateTimeKind.Utc),
                TimeZoneInfo.Utc);

            Assert.Equal(new DateTime(2026, 4, 30, 9, 0, 0, DateTimeKind.Utc), nextRun);
        }

        [Fact]
        public void CalculateNextRunAtUtc_AsiaKolkata_StoresUtcForLocalSchedule()
        {
            var pref = CreateScheduledPreference(Guid.NewGuid(), DateTime.UtcNow);
            pref.Frequency = EmailSummaryFrequency.Daily;
            pref.TimeOfDay = new TimeOnly(9, 0);
            pref.TimeZoneId = "Asia/Kolkata";

            var kolkata = FindTimeZone("Asia/Kolkata", "India Standard Time");
            var nextRun = EmailSummaryService.CalculateNextRunAtUtc(
                pref,
                new DateTime(2026, 5, 15, 2, 0, 0, DateTimeKind.Utc),
                kolkata);

            Assert.Equal(new DateTime(2026, 5, 15, 3, 30, 0, DateTimeKind.Utc), nextRun);
        }
    }
}
