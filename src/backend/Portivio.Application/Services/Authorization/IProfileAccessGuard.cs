using Microsoft.EntityFrameworkCore;
using Portivio.Application.Results;
using Portivio.Domain.Entities;
using Portivio.Infrastructure.Data;

namespace Portivio.Application.Services.Authorization
{
    public interface IProfileAccessGuard
    {
        Task<Result<Profile>> GetOwnedAsync(Guid userId, Guid profileId, CancellationToken ct = default);
        Task<Result> EnsureOwnerAsync(Guid userId, Guid profileId, CancellationToken ct = default);
    }

    public class ProfileAccessGuard : IProfileAccessGuard
    {
        private readonly PortivioDbContext _context;

        public ProfileAccessGuard(PortivioDbContext context)
        {
            _context = context;
        }

        public async Task<Result<Profile>> GetOwnedAsync(Guid userId, Guid profileId, CancellationToken ct = default)
        {
            var profile = await _context.Profiles.AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == profileId, ct);

            if (profile == null)
                return Result<Profile>.NotFound("Profile not found");

            if (profile.UserId != userId)
                return Result<Profile>.Forbidden("Access denied");

            return Result<Profile>.Success(profile);
        }

        public async Task<Result> EnsureOwnerAsync(Guid userId, Guid profileId, CancellationToken ct = default)
        {
            var owned = await GetOwnedAsync(userId, profileId, ct);
            return owned.IsSuccess ? Result.Success() : owned.ToFailure();
        }
    }
}
