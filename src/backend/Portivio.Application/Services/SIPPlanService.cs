using Microsoft.EntityFrameworkCore;
using Portivio.Application.DTOs.SIPPlan;
using Portivio.Application.Results;
using Portivio.Domain.Entities;
using Portivio.Infrastructure.Data;

namespace Portivio.Application.Services
{
    public interface ISIPPlanService
    {
        Task<Result<List<SIPPlanResponse>>> GetSIPPlansAsync(Guid userId, Guid profileId, bool? activeOnly = null);
        Task<Result<SIPPlanResponse>> CreateSIPPlanAsync(Guid userId, Guid profileId, CreateSIPPlanRequest request);
        Task<Result<SIPPlanResponse>> UpdateSIPPlanAsync(Guid userId, Guid profileId, Guid sipId, UpdateSIPPlanRequest request);
        Task<Result<SIPPlanResponse>> ActivateSIPPlanAsync(Guid userId, Guid profileId, Guid sipId);
        Task<Result<SIPPlanResponse>> DeactivateSIPPlanAsync(Guid userId, Guid profileId, Guid sipId);
        Task<Result> DeleteSIPPlanAsync(Guid userId, Guid profileId, Guid sipId);
    }

    public class SIPPlanService : ISIPPlanService
    {
        private readonly PortivioDbContext _context;

        public SIPPlanService(PortivioDbContext context)
        {
            _context = context;
        }

        public async Task<Result<List<SIPPlanResponse>>> GetSIPPlansAsync(Guid userId, Guid profileId, bool? activeOnly = null)
        {
            try
            {
                var profile = await _context.Profiles.AsNoTracking().FirstOrDefaultAsync(p => p.Id == profileId);
                if (profile == null)
                    return Result<List<SIPPlanResponse>>.NotFound("Profile not found");
                if (profile.UserId != userId)
                    return Result<List<SIPPlanResponse>>.Forbidden("Access denied");

                var query = _context.SIPPlans
                    .Include(s => s.Instrument)
                    .Where(s => s.ProfileId == profileId);

                if (activeOnly.HasValue)
                    query = query.Where(s => s.IsActive == activeOnly.Value);

                var plans = await query
                    .OrderByDescending(s => s.CreatedAt)
                    .Select(s => MapToResponse(s))
                    .ToListAsync();

                return Result<List<SIPPlanResponse>>.Success(plans, "SIP plans retrieved successfully");
            }
            catch (Exception ex)
            {
                return Result<List<SIPPlanResponse>>.InternalServerError($"Error retrieving SIP plans: {ex.Message}");
            }
        }

        public async Task<Result<SIPPlanResponse>> CreateSIPPlanAsync(Guid userId, Guid profileId, CreateSIPPlanRequest request)
        {
            try
            {
                var validationError = ValidateRequest(request.Amount, request.SIPDay, request.StartDate, request.EndDate);
                if (validationError != null)
                    return Result<SIPPlanResponse>.BadRequest(validationError);

                var profile = await _context.Profiles.AsNoTracking().FirstOrDefaultAsync(p => p.Id == profileId);
                if (profile == null)
                    return Result<SIPPlanResponse>.NotFound("Profile not found");
                if (profile.UserId != userId)
                    return Result<SIPPlanResponse>.Forbidden("Access denied");

                var instrument = await _context.Instruments.FirstOrDefaultAsync(i => i.Id == request.InstrumentId);
                if (instrument == null)
                    return Result<SIPPlanResponse>.BadRequest("Instrument not found");

                var plan = new SIPPlan
                {
                    Id = Guid.NewGuid(),
                    ProfileId = profileId,
                    InstrumentId = request.InstrumentId,
                    Amount = request.Amount,
                    SIPDay = request.SIPDay,
                    StartDate = request.StartDate.ToUniversalTime(),
                    EndDate = request.EndDate.ToUniversalTime(),
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };

                _context.SIPPlans.Add(plan);
                await _context.SaveChangesAsync();

                return Result<SIPPlanResponse>.Success(new SIPPlanResponse
                {
                    Id = plan.Id,
                    ProfileId = plan.ProfileId,
                    InstrumentId = plan.InstrumentId,
                    InstrumentName = instrument.Name,
                    InstrumentSymbol = instrument.Symbol,
                    Amount = plan.Amount,
                    SIPDay = plan.SIPDay,
                    StartDate = plan.StartDate,
                    EndDate = plan.EndDate,
                    IsActive = plan.IsActive,
                    CreatedAt = plan.CreatedAt
                }, "SIP plan created successfully", 201);
            }
            catch (Exception ex)
            {
                return Result<SIPPlanResponse>.InternalServerError($"Error creating SIP plan: {ex.Message}");
            }
        }

        public async Task<Result<SIPPlanResponse>> UpdateSIPPlanAsync(Guid userId, Guid profileId, Guid sipId, UpdateSIPPlanRequest request)
        {
            try
            {
                var validationError = ValidateRequest(request.Amount, request.SIPDay, request.StartDate, request.EndDate);
                if (validationError != null)
                    return Result<SIPPlanResponse>.BadRequest(validationError);

                var (plan, error) = await LoadPlanWithOwnershipCheck<SIPPlanResponse>(userId, profileId, sipId);
                if (error != null) return error;

                plan!.Amount = request.Amount;
                plan.SIPDay = request.SIPDay;
                plan.StartDate = request.StartDate.ToUniversalTime();
                plan.EndDate = request.EndDate.ToUniversalTime();

                await _context.SaveChangesAsync();
                await _context.Entry(plan).Reference(p => p.Instrument).LoadAsync();

                return Result<SIPPlanResponse>.Success(MapToResponse(plan), "SIP plan updated successfully");
            }
            catch (Exception ex)
            {
                return Result<SIPPlanResponse>.InternalServerError($"Error updating SIP plan: {ex.Message}");
            }
        }

        public async Task<Result<SIPPlanResponse>> ActivateSIPPlanAsync(Guid userId, Guid profileId, Guid sipId)
        {
            try
            {
                var (plan, error) = await LoadPlanWithOwnershipCheck<SIPPlanResponse>(userId, profileId, sipId);
                if (error != null) return error;

                plan!.IsActive = true;
                await _context.SaveChangesAsync();
                await _context.Entry(plan).Reference(p => p.Instrument).LoadAsync();

                return Result<SIPPlanResponse>.Success(MapToResponse(plan), "SIP plan activated successfully");
            }
            catch (Exception ex)
            {
                return Result<SIPPlanResponse>.InternalServerError($"Error activating SIP plan: {ex.Message}");
            }
        }

        public async Task<Result<SIPPlanResponse>> DeactivateSIPPlanAsync(Guid userId, Guid profileId, Guid sipId)
        {
            try
            {
                var (plan, error) = await LoadPlanWithOwnershipCheck<SIPPlanResponse>(userId, profileId, sipId);
                if (error != null) return error;

                plan!.IsActive = false;
                await _context.SaveChangesAsync();
                await _context.Entry(plan).Reference(p => p.Instrument).LoadAsync();

                return Result<SIPPlanResponse>.Success(MapToResponse(plan), "SIP plan deactivated successfully");
            }
            catch (Exception ex)
            {
                return Result<SIPPlanResponse>.InternalServerError($"Error deactivating SIP plan: {ex.Message}");
            }
        }

        public async Task<Result> DeleteSIPPlanAsync(Guid userId, Guid profileId, Guid sipId)
        {
            try
            {
                var profile = await _context.Profiles.AsNoTracking().FirstOrDefaultAsync(p => p.Id == profileId);
                if (profile == null)
                    return Result.NotFound("Profile not found");
                if (profile.UserId != userId)
                    return Result.Forbidden("Access denied");

                var plan = await _context.SIPPlans.FirstOrDefaultAsync(s => s.Id == sipId && s.ProfileId == profileId);
                if (plan == null)
                    return Result.NotFound("SIP plan not found");

                _context.SIPPlans.Remove(plan);
                await _context.SaveChangesAsync();

                return Result.Success("SIP plan deleted successfully");
            }
            catch (Exception ex)
            {
                return Result.InternalServerError($"Error deleting SIP plan: {ex.Message}");
            }
        }

        private async Task<(SIPPlan? plan, Result<T>? error)> LoadPlanWithOwnershipCheck<T>(Guid userId, Guid profileId, Guid sipId)
        {
            var profile = await _context.Profiles.AsNoTracking().FirstOrDefaultAsync(p => p.Id == profileId);
            if (profile == null)
                return (null, Result<T>.NotFound("Profile not found"));
            if (profile.UserId != userId)
                return (null, Result<T>.Forbidden("Access denied"));

            var plan = await _context.SIPPlans.FirstOrDefaultAsync(s => s.Id == sipId && s.ProfileId == profileId);
            if (plan == null)
                return (null, Result<T>.NotFound("SIP plan not found"));

            return (plan, null);
        }

        private static string? ValidateRequest(decimal amount, int sipDay, DateTime startDate, DateTime endDate)
        {
            if (amount <= 0)
                return "Amount must be greater than zero";
            if (sipDay < 1 || sipDay > 28)
                return "SIPDay must be between 1 and 28";
            if (endDate <= startDate)
                return "EndDate must be after StartDate";
            return null;
        }

        private static SIPPlanResponse MapToResponse(SIPPlan s) => new()
        {
            Id = s.Id,
            ProfileId = s.ProfileId,
            InstrumentId = s.InstrumentId,
            InstrumentName = s.Instrument.Name,
            InstrumentSymbol = s.Instrument.Symbol,
            Amount = s.Amount,
            SIPDay = s.SIPDay,
            StartDate = s.StartDate,
            EndDate = s.EndDate,
            IsActive = s.IsActive,
            CreatedAt = s.CreatedAt
        };
    }
}
