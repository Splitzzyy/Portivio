using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Portivio.Application.DTOs.Profile;
using Portivio.Application.Services;
using Portivio.Domain.Entities;
using Portivio.Infrastructure.Data;
using Xunit;

namespace Portivio.Tests.Services
{
    public class ProfileServiceTests
    {
        private PortivioDbContext CreateInMemoryDbContext()
        {
            var options = new DbContextOptionsBuilder<PortivioDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
            return new PortivioDbContext(options);
        }

        private static ILogger<ProfileService> CreateMockLogger() => new Mock<ILogger<ProfileService>>().Object;

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

        private static Profile CreateProfile(Guid userId, Guid? id = null) => new()
        {
            Id = id ?? Guid.NewGuid(),
            UserId = userId,
            Name = "Test Portfolio",
            BaseCurrency = "USD",
            Description = "Test",
            CreatedAt = DateTime.UtcNow
        };

        [Fact]
        public async Task CreateProfile_ValidRequest_ReturnsSuccess()
        {
            using var context = CreateInMemoryDbContext();
            var user = CreateUser();
            context.Users.Add(user);
            await context.SaveChangesAsync();

            var service = new ProfileService(context, CreateMockLogger());
            var result = await service.CreateProfileAsync(user.Id, new CreateProfileRequest
            {
                Name = "My Portfolio",
                BaseCurrency = "USD",
                Description = "Test"
            });

            Assert.True(result.IsSuccess);
            Assert.Equal(201, result.StatusCode);
            Assert.NotNull(result.Data);
            Assert.Equal("My Portfolio", result.Data!.Name);
            Assert.Equal("USD", result.Data.BaseCurrency);
            Assert.Equal(user.Id, result.Data.UserId);
        }

        [Fact]
        public async Task CreateProfile_EmptyName_ReturnsBadRequest()
        {
            using var context = CreateInMemoryDbContext();
            var user = CreateUser();
            context.Users.Add(user);
            await context.SaveChangesAsync();

            var service = new ProfileService(context, CreateMockLogger());
            var result = await service.CreateProfileAsync(user.Id, new CreateProfileRequest
            {
                Name = "",
                BaseCurrency = "USD"
            });

            Assert.False(result.IsSuccess);
            Assert.Equal(400, result.StatusCode);
        }

        [Fact]
        public async Task CreateProfile_InvalidCurrency_ReturnsBadRequest()
        {
            using var context = CreateInMemoryDbContext();
            var user = CreateUser();
            context.Users.Add(user);
            await context.SaveChangesAsync();

            var service = new ProfileService(context, CreateMockLogger());
            var result = await service.CreateProfileAsync(user.Id, new CreateProfileRequest
            {
                Name = "Portfolio",
                BaseCurrency = "USDD"
            });

            Assert.False(result.IsSuccess);
            Assert.Equal(400, result.StatusCode);
        }

        [Fact]
        public async Task UpdateProfile_OtherUsersProfile_ReturnsForbidden()
        {
            using var context = CreateInMemoryDbContext();
            var user1 = CreateUser();
            var user2 = CreateUser();
            var profile = CreateProfile(user1.Id);
            context.Users.AddRange(user1, user2);
            context.Profiles.Add(profile);
            await context.SaveChangesAsync();

            var service = new ProfileService(context, CreateMockLogger());
            var result = await service.UpdateProfileAsync(user2.Id, profile.Id, new UpdateProfileRequest
            {
                Name = "Hacked",
                BaseCurrency = "USD"
            });

            Assert.False(result.IsSuccess);
            Assert.Equal(403, result.StatusCode);
        }

        [Fact]
        public async Task UpdateProfile_NonexistentProfile_ReturnsNotFound()
        {
            using var context = CreateInMemoryDbContext();
            var user = CreateUser();
            context.Users.Add(user);
            await context.SaveChangesAsync();

            var service = new ProfileService(context, CreateMockLogger());
            var result = await service.UpdateProfileAsync(user.Id, Guid.NewGuid(), new UpdateProfileRequest
            {
                Name = "Updated",
                BaseCurrency = "EUR"
            });

            Assert.False(result.IsSuccess);
            Assert.Equal(404, result.StatusCode);
        }

        [Fact]
        public async Task DeleteProfile_WithHoldings_ReturnsConflict()
        {
            using var context = CreateInMemoryDbContext();
            var user = CreateUser();
            var profile = CreateProfile(user.Id);
            var assetType = new AssetType { Id = Guid.NewGuid(), Name = "Equity" };
            var instrument = new Instrument
            {
                Id = Guid.NewGuid(),
                AssetTypeId = assetType.Id,
                Name = "Test Corp",
                Symbol = "TEST",
                Currency = "USD"
            };
            var holding = new Holding
            {
                Id = Guid.NewGuid(),
                ProfileId = profile.Id,
                InstrumentId = instrument.Id,
                Quantity = 10m,
                AvgPrice = 100m,
                CurrentPrice = 110m,
                MarketValue = 1100m,
                UnrealizedPnL = 100m,
                LastUpdated = DateTime.UtcNow
            };

            context.Users.Add(user);
            context.Profiles.Add(profile);
            context.AssetTypes.Add(assetType);
            context.Instruments.Add(instrument);
            context.Holdings.Add(holding);
            await context.SaveChangesAsync();

            var service = new ProfileService(context, CreateMockLogger());
            var result = await service.DeleteProfileAsync(user.Id, profile.Id);

            Assert.False(result.IsSuccess);
            Assert.Equal(409, result.StatusCode);
        }

        [Fact]
        public async Task DeleteProfile_NoChildren_ReturnsSuccess()
        {
            using var context = CreateInMemoryDbContext();
            var user = CreateUser();
            var profile = CreateProfile(user.Id);
            context.Users.Add(user);
            context.Profiles.Add(profile);
            await context.SaveChangesAsync();

            var service = new ProfileService(context, CreateMockLogger());
            var result = await service.DeleteProfileAsync(user.Id, profile.Id);

            Assert.True(result.IsSuccess);
            Assert.False(await context.Profiles.AnyAsync(p => p.Id == profile.Id));
        }

        [Fact]
        public async Task DeleteProfile_OtherUsersProfile_ReturnsForbidden()
        {
            using var context = CreateInMemoryDbContext();
            var user1 = CreateUser();
            var user2 = CreateUser();
            var profile = CreateProfile(user1.Id);
            context.Users.AddRange(user1, user2);
            context.Profiles.Add(profile);
            await context.SaveChangesAsync();

            var service = new ProfileService(context, CreateMockLogger());
            var result = await service.DeleteProfileAsync(user2.Id, profile.Id);

            Assert.False(result.IsSuccess);
            Assert.Equal(403, result.StatusCode);
        }

        [Fact]
        public async Task GetProfiles_ReturnsOnlyCurrentUsersProfiles()
        {
            using var context = CreateInMemoryDbContext();
            var user1 = CreateUser();
            var user2 = CreateUser();
            var profile1 = CreateProfile(user1.Id);
            var profile2 = CreateProfile(user2.Id);
            context.Users.AddRange(user1, user2);
            context.Profiles.AddRange(profile1, profile2);
            await context.SaveChangesAsync();

            var service = new ProfileService(context, CreateMockLogger());
            var result = await service.GetProfilesAsync(user1.Id);

            Assert.True(result.IsSuccess);
            Assert.Single(result.Data!);
            Assert.All(result.Data!, p => Assert.Equal(user1.Id, p.UserId));
        }
    }
}
