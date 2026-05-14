using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Portivio.Application.DTOs.EmailSummary;
using Portivio.Application.Services;
using Portivio.Domain.Entities;
using Portivio.Domain.Enums;
using Portivio.Infrastructure.Data;
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

        [Fact]
        public async Task GetPreference_WhenMissing_ReturnsDefaultAndPersists()
        {
            using var context = CreateInMemoryDbContext();
            var user = CreateUser();
            context.Users.Add(user);
            await context.SaveChangesAsync();

            var service = new EmailSummaryService(context, CreateMockLogger());
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

            var service = new EmailSummaryService(context, CreateMockLogger());
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

            var service = new EmailSummaryService(context, CreateMockLogger());
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

            var service = new EmailSummaryService(context, CreateMockLogger());
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

            var service = new EmailSummaryService(context, CreateMockLogger());
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

            var service = new EmailSummaryService(context, CreateMockLogger());
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

            var service = new EmailSummaryService(context, CreateMockLogger());
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

            var service = new EmailSummaryService(context, CreateMockLogger());
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
    }
}
