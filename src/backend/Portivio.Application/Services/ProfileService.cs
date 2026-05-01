using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
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
        private readonly ILogger<ProfileService> _logger;

        public ProfileService(PortivioDbContext context, ILogger<ProfileService> logger)
        {
            _context = context;
            _logger = logger;
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
                _logger.LogError(ex, "Error retrieving profiles. UserId={UserId}", userId);
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
                {
                    _logger.LogWarning("Profile creation rejected: user not found. UserId={UserId}", userId);
                    return Result<ProfileResponse>.NotFound("User not found");
                }

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

                _logger.LogInformation("Profile created. ProfileId={ProfileId} UserId={UserId} Name={Name}",
                    profile.Id, userId, profile.Name);

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
                _logger.LogError(ex, "Error creating profile. UserId={UserId}", userId);
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
                {
                    _logger.LogWarning("Profile update rejected: not found. ProfileId={ProfileId} UserId={UserId}", profileId, userId);
                    return Result<ProfileResponse>.NotFound("Profile not found");
                }

                if (profile.UserId != userId)
                {
                    _logger.LogWarning("Profile update rejected: ownership mismatch. ProfileId={ProfileId} OwnerId={OwnerId} CallerId={CallerId}",
                        profileId, profile.UserId, userId);
                    return Result<ProfileResponse>.Forbidden("Access denied");
                }

                profile.Name = request.Name.Trim();
                profile.BaseCurrency = request.BaseCurrency.ToUpperInvariant();
                profile.Description = request.Description?.Trim() ?? string.Empty;

                await _context.SaveChangesAsync();

                _logger.LogInformation("Profile updated. ProfileId={ProfileId} UserId={UserId}", profileId, userId);

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
                _logger.LogError(ex, "Error updating profile. ProfileId={ProfileId} UserId={UserId}", profileId, userId);
                return Result<ProfileResponse>.InternalServerError($"Error updating profile: {ex.Message}");
            }
        }

        public async Task<Result> DeleteProfileAsync(Guid userId, Guid profileId)
        {
            try
            {
                var profile = await _context.Profiles.FirstOrDefaultAsync(p => p.Id == profileId);
                if (profile == null)
                {
                    _logger.LogWarning("Profile delete rejected: not found. ProfileId={ProfileId} UserId={UserId}", profileId, userId);
                    return Result.NotFound("Profile not found");
                }

                if (profile.UserId != userId)
                {
                    _logger.LogWarning("Profile delete rejected: ownership mismatch. ProfileId={ProfileId} OwnerId={OwnerId} CallerId={CallerId}",
                        profileId, profile.UserId, userId);
                    return Result.Forbidden("Access denied");
                }

                var hasHoldings = await _context.Holdings.AnyAsync(h => h.ProfileId == profileId);
                if (hasHoldings)
                {
                    _logger.LogWarning("Profile delete rejected: holdings exist. ProfileId={ProfileId}", profileId);
                    return Result.Conflict("Profile has associated holdings. Remove them first.");
                }

                var hasTransactions = await _context.Transactions.AnyAsync(t => t.ProfileId == profileId);
                if (hasTransactions)
                {
                    _logger.LogWarning("Profile delete rejected: transactions exist. ProfileId={ProfileId}", profileId);
                    return Result.Conflict("Profile has associated transactions. Remove them first.");
                }

                _context.Profiles.Remove(profile);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Profile deleted. ProfileId={ProfileId} UserId={UserId}", profileId, userId);

                return Result.Success("Profile deleted successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting profile. ProfileId={ProfileId} UserId={UserId}", profileId, userId);
                return Result.InternalServerError($"Error deleting profile: {ex.Message}");
            }
        }
    }
}
