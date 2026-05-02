using Microsoft.EntityFrameworkCore;
using Portivio.Application.DTOs.SIPPlan;
using Portivio.Application.Services;
using Portivio.Application.Services.Authorization;
using Portivio.Domain.Entities;
using Portivio.Infrastructure.Data;
using Xunit;

namespace Portivio.Tests.Services
{
    public class SIPPlanServiceTests
    {
        private PortivioDbContext CreateInMemoryDbContext()
        {
            var options = new DbContextOptionsBuilder<PortivioDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
            return new PortivioDbContext(options);
        }

        private static (User user, Profile profile, Instrument instrument) SeedBasicData(PortivioDbContext context)
        {
            var user = new User { Id = Guid.NewGuid(), Email = $"u-{Guid.NewGuid()}@t.com", Name = "U", PasswordHash = "h", IsVerified = true, IsActive = true, CreatedAt = DateTime.UtcNow };
            var profile = new Profile { Id = Guid.NewGuid(), UserId = user.Id, Name = "P", BaseCurrency = "USD", Description = "", CreatedAt = DateTime.UtcNow };
            var assetType = new AssetType { Id = Guid.NewGuid(), Name = "Equity" };
            var instrument = new Instrument { Id = Guid.NewGuid(), AssetTypeId = assetType.Id, Name = "Test Corp", Symbol = "TEST", Currency = "USD" };
            context.Users.Add(user);
            context.Profiles.Add(profile);
            context.AssetTypes.Add(assetType);
            context.Instruments.Add(instrument);
            context.SaveChanges();
            return (user, profile, instrument);
        }

        private static CreateSIPPlanRequest ValidRequest(Guid instrumentId) => new()
        {
            InstrumentId = instrumentId,
            Amount = 5000m,
            SIPDay = 10,
            StartDate = DateTime.UtcNow,
            EndDate = DateTime.UtcNow.AddYears(1)
        };

        [Fact]
        public async Task CreateSIPPlan_ValidRequest_IsActiveByDefault()
        {
            using var context = CreateInMemoryDbContext();
            var (user, profile, instrument) = SeedBasicData(context);
            var service = new SIPPlanService(context, new ProfileAccessGuard(context));

            var result = await service.CreateSIPPlanAsync(user.Id, profile.Id, ValidRequest(instrument.Id));

            Assert.True(result.IsSuccess);
            Assert.Equal(201, result.StatusCode);
            Assert.True(result.Data!.IsActive);
        }

        [Fact]
        public async Task CreateSIPPlan_InvalidSIPDay_Zero_ReturnsBadRequest()
        {
            using var context = CreateInMemoryDbContext();
            var (user, profile, instrument) = SeedBasicData(context);
            var service = new SIPPlanService(context, new ProfileAccessGuard(context));

            var req = ValidRequest(instrument.Id);
            req.SIPDay = 0;
            var result = await service.CreateSIPPlanAsync(user.Id, profile.Id, req);

            Assert.False(result.IsSuccess);
            Assert.Equal(400, result.StatusCode);
        }

        [Fact]
        public async Task CreateSIPPlan_InvalidSIPDay_29_ReturnsBadRequest()
        {
            using var context = CreateInMemoryDbContext();
            var (user, profile, instrument) = SeedBasicData(context);
            var service = new SIPPlanService(context, new ProfileAccessGuard(context));

            var req = ValidRequest(instrument.Id);
            req.SIPDay = 29;
            var result = await service.CreateSIPPlanAsync(user.Id, profile.Id, req);

            Assert.False(result.IsSuccess);
            Assert.Equal(400, result.StatusCode);
        }

        [Fact]
        public async Task CreateSIPPlan_EndBeforeStart_ReturnsBadRequest()
        {
            using var context = CreateInMemoryDbContext();
            var (user, profile, instrument) = SeedBasicData(context);
            var service = new SIPPlanService(context, new ProfileAccessGuard(context));

            var req = ValidRequest(instrument.Id);
            req.StartDate = DateTime.UtcNow.AddYears(1);
            req.EndDate = DateTime.UtcNow;
            var result = await service.CreateSIPPlanAsync(user.Id, profile.Id, req);

            Assert.False(result.IsSuccess);
            Assert.Equal(400, result.StatusCode);
        }

        [Fact]
        public async Task ActivateSIPPlan_AlreadyActive_ReturnsSuccess()
        {
            using var context = CreateInMemoryDbContext();
            var (user, profile, instrument) = SeedBasicData(context);
            var service = new SIPPlanService(context, new ProfileAccessGuard(context));
            var createResult = await service.CreateSIPPlanAsync(user.Id, profile.Id, ValidRequest(instrument.Id));

            var result = await service.ActivateSIPPlanAsync(user.Id, profile.Id, createResult.Data!.Id);

            Assert.True(result.IsSuccess);
            Assert.True(result.Data!.IsActive);
        }

        [Fact]
        public async Task DeactivateSIPPlan_SetsIsActiveFalse()
        {
            using var context = CreateInMemoryDbContext();
            var (user, profile, instrument) = SeedBasicData(context);
            var service = new SIPPlanService(context, new ProfileAccessGuard(context));
            var createResult = await service.CreateSIPPlanAsync(user.Id, profile.Id, ValidRequest(instrument.Id));

            var result = await service.DeactivateSIPPlanAsync(user.Id, profile.Id, createResult.Data!.Id);

            Assert.True(result.IsSuccess);
            Assert.False(result.Data!.IsActive);
        }

        [Fact]
        public async Task UpdateSIPPlan_OtherUsersProfile_ReturnsForbidden()
        {
            using var context = CreateInMemoryDbContext();
            var (user1, profile, instrument) = SeedBasicData(context);
            var user2 = new User { Id = Guid.NewGuid(), Email = "u2@t.com", Name = "U2", PasswordHash = "h", IsVerified = true, IsActive = true, CreatedAt = DateTime.UtcNow };
            context.Users.Add(user2);
            await context.SaveChangesAsync();

            var service = new SIPPlanService(context, new ProfileAccessGuard(context));
            var createResult = await service.CreateSIPPlanAsync(user1.Id, profile.Id, ValidRequest(instrument.Id));

            var req = new UpdateSIPPlanRequest { Amount = 1000m, SIPDay = 5, StartDate = DateTime.UtcNow, EndDate = DateTime.UtcNow.AddYears(1) };
            var result = await service.UpdateSIPPlanAsync(user2.Id, profile.Id, createResult.Data!.Id, req);

            Assert.False(result.IsSuccess);
            Assert.Equal(403, result.StatusCode);
        }

        [Fact]
        public async Task GetSIPPlans_ActiveOnlyFilter_ReturnsOnlyActive()
        {
            using var context = CreateInMemoryDbContext();
            var (user, profile, instrument) = SeedBasicData(context);
            var service = new SIPPlanService(context, new ProfileAccessGuard(context));
            var plan1 = await service.CreateSIPPlanAsync(user.Id, profile.Id, ValidRequest(instrument.Id));
            await service.DeactivateSIPPlanAsync(user.Id, profile.Id, plan1.Data!.Id);
            await service.CreateSIPPlanAsync(user.Id, profile.Id, ValidRequest(instrument.Id));

            var result = await service.GetSIPPlansAsync(user.Id, profile.Id, activeOnly: true);

            Assert.True(result.IsSuccess);
            Assert.Single(result.Data!);
            Assert.All(result.Data!, p => Assert.True(p.IsActive));
        }
    }
}
