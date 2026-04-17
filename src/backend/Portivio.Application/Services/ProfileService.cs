using Microsoft.EntityFrameworkCore;
using Portivio.Application.DTOs.Profile;
using Portivio.Application.Results;
using Portivio.Domain.Entities;
using Portivio.Infrastructure.Data;

namespace Portivio.Application.Services
{
    public interface IProfileService
    {
        Task<Result<List<ProfileResponse>>> GetProfilesAsync(Guid userId);
        Task<Result<ProfileResponse>> CreateProfileAsync(Guid userId, CreateProfileRequest request);
        Task<Result<ProfileResponse>> UpdateProfileAsync(Guid userId, Guid profileId, UpdateProfileRequest request);
        Task<Result> DeleteProfileAsync(Guid userId, Guid profileId);
    }

    public class ProfileService : IProfileService
    {
        private readonly PortivioDbContext _context;

        public ProfileService(PortivioDbContext context)
        {
            _context = context;
        }

        public async Task<Result<List<ProfileResponse>>> GetProfilesAsync(Guid userId)
        {
            try
            {
                var profiles = await _context.Profiles
                    .Where(p => p.UserId == userId)
                    .OrderBy(p => p.CreatedAt)
                    .Select(p => new ProfileResponse
                    {
                        Id = p.Id,
                        UserId = p.UserId,
                        Name = p.Name,
                        BaseCurrency = p.BaseCurrency,
                        Description = p.Description,
                        CreatedAt = p.CreatedAt
                    })
                    .ToListAsync();

                return Result<List<ProfileResponse>>.Success(profiles, "Profiles retrieved successfully");
            }
            catch (Exception ex)
            {
                return Result<List<ProfileResponse>>.InternalServerError($"Error retrieving profiles: {ex.Message}");
            }
        }

        public async Task<Result<ProfileResponse>> CreateProfileAsync(Guid userId, CreateProfileRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.Name))
                    return Result<ProfileResponse>.BadRequest("Profile name is required");

                if (string.IsNullOrWhiteSpace(request.BaseCurrency) || request.BaseCurrency.Length != 3)
                    return Result<ProfileResponse>.BadRequest("BaseCurrency must be a 3-character ISO currency code");

                var userExists = await _context.Users.AnyAsync(u => u.Id == userId);
                if (!userExists)
                    return Result<ProfileResponse>.NotFound("User not found");

                var profile = new Profile
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    Name = request.Name.Trim(),
                    BaseCurrency = request.BaseCurrency.ToUpperInvariant(),
                    Description = request.Description?.Trim() ?? string.Empty,
                    CreatedAt = DateTime.UtcNow
                };

                _context.Profiles.Add(profile);
                await _context.SaveChangesAsync();

                return Result<ProfileResponse>.Success(new ProfileResponse
                {
                    Id = profile.Id,
                    UserId = profile.UserId,
                    Name = profile.Name,
                    BaseCurrency = profile.BaseCurrency,
                    Description = profile.Description,
                    CreatedAt = profile.CreatedAt
                }, "Profile created successfully", 201);
            }
            catch (Exception ex)
            {
                return Result<ProfileResponse>.InternalServerError($"Error creating profile: {ex.Message}");
            }
        }

        public async Task<Result<ProfileResponse>> UpdateProfileAsync(Guid userId, Guid profileId, UpdateProfileRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.Name))
                    return Result<ProfileResponse>.BadRequest("Profile name is required");

                if (string.IsNullOrWhiteSpace(request.BaseCurrency) || request.BaseCurrency.Length != 3)
                    return Result<ProfileResponse>.BadRequest("BaseCurrency must be a 3-character ISO currency code");

                var profile = await _context.Profiles.FirstOrDefaultAsync(p => p.Id == profileId);
                if (profile == null)
                    return Result<ProfileResponse>.NotFound("Profile not found");

                if (profile.UserId != userId)
                    return Result<ProfileResponse>.Forbidden("Access denied");

                profile.Name = request.Name.Trim();
                profile.BaseCurrency = request.BaseCurrency.ToUpperInvariant();
                profile.Description = request.Description?.Trim() ?? string.Empty;

                await _context.SaveChangesAsync();

                return Result<ProfileResponse>.Success(new ProfileResponse
                {
                    Id = profile.Id,
                    UserId = profile.UserId,
                    Name = profile.Name,
                    BaseCurrency = profile.BaseCurrency,
                    Description = profile.Description,
                    CreatedAt = profile.CreatedAt
                }, "Profile updated successfully");
            }
            catch (Exception ex)
            {
                return Result<ProfileResponse>.InternalServerError($"Error updating profile: {ex.Message}");
            }
        }

        public async Task<Result> DeleteProfileAsync(Guid userId, Guid profileId)
        {
            try
            {
                var profile = await _context.Profiles.FirstOrDefaultAsync(p => p.Id == profileId);
                if (profile == null)
                    return Result.NotFound("Profile not found");

                if (profile.UserId != userId)
                    return Result.Forbidden("Access denied");

                var hasHoldings = await _context.Holdings.AnyAsync(h => h.ProfileId == profileId);
                if (hasHoldings)
                    return Result.Conflict("Profile has associated holdings. Remove them first.");

                var hasTransactions = await _context.Transactions.AnyAsync(t => t.ProfileId == profileId);
                if (hasTransactions)
                    return Result.Conflict("Profile has associated transactions. Remove them first.");

                _context.Profiles.Remove(profile);
                await _context.SaveChangesAsync();

                return Result.Success("Profile deleted successfully");
            }
            catch (Exception ex)
            {
                return Result.InternalServerError($"Error deleting profile: {ex.Message}");
            }
        }
    }
}
