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

        private static IOptions<EmailSummaryOptions> CreateEmailSummaryOptions(int cooldownMinutes = 10)
            => Options.Create(new EmailSummaryOptions { ManualQueueCooldownMinutes = cooldownMinutes, MaxLockMinutes = 15 });

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
    }
}
