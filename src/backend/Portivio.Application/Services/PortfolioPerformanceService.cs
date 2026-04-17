using Microsoft.EntityFrameworkCore;
using Portivio.Application.DTOs.PortfolioPerformance;
using Portivio.Application.Results;
using Portivio.Domain.Entities;
using Portivio.Domain.Enums;
using Portivio.Infrastructure.Data;

namespace Portivio.Application.Services
{
    public interface IPortfolioPerformanceService
    {
        Task<Result<PerformanceResponse>> RecordSnapshotAsync(Guid userId, Guid profileId, RecordSnapshotRequest? request = null);
        Task<Result<PerformanceHistoryResponse>> GetPerformanceHistoryAsync(Guid userId, Guid profileId, int days = 90);
        Task<Result<PerformanceResponse>> GetLatestPerformanceAsync(Guid userId, Guid profileId);
    }

    public class PortfolioPerformanceService : IPortfolioPerformanceService
    {
        private readonly PortivioDbContext _context;

        public PortfolioPerformanceService(PortivioDbContext context)
        {
            _context = context;
        }

        public async Task<Result<PerformanceResponse>> RecordSnapshotAsync(Guid userId, Guid profileId, RecordSnapshotRequest? request = null)
        {
            try
            {
                var profile = await _context.Profiles.AsNoTracking().FirstOrDefaultAsync(p => p.Id == profileId);
                if (profile == null)
                    return Result<PerformanceResponse>.NotFound("Profile not found");
                if (profile.UserId != userId)
                    return Result<PerformanceResponse>.Forbidden("Access denied");

                var snapshotDate = (request?.Date ?? DateTime.UtcNow).Date;
                var snapshotDateUtc = new DateTime(snapshotDate.Year, snapshotDate.Month, snapshotDate.Day, 0, 0, 0, DateTimeKind.Utc);

                var holdings = await _context.Holdings.Where(h => h.ProfileId == profileId).ToListAsync();
                var totalInvestment = holdings.Sum(h => h.Quantity * h.AvgPrice);
                var currentValue = holdings.Sum(h => h.MarketValue);

                var yesterday = snapshotDateUtc.AddDays(-1);
                var previousSnapshot = await _context.PortfolioPerformances
                    .Where(pp => pp.ProfileId == profileId && pp.Date < snapshotDateUtc)
                    .OrderByDescending(pp => pp.Date)
                    .FirstOrDefaultAsync();

                var dayChange = currentValue - (previousSnapshot?.CurrentValue ?? currentValue);
                var totalReturn = currentValue - totalInvestment;

                var transactions = await _context.Transactions
                    .Where(t => t.ProfileId == profileId)
                    .OrderBy(t => t.TransactionDate)
                    .ToListAsync();

                var cashFlows = BuildCashFlows(transactions, currentValue, snapshotDateUtc);
                var xirr = XirrCalculator.Calculate(cashFlows);

                var existing = await _context.PortfolioPerformances
                    .FirstOrDefaultAsync(pp => pp.ProfileId == profileId && pp.Date == snapshotDateUtc);

                PortfolioPerformance snapshot;
                if (existing != null)
                {
                    existing.TotalInvestment = totalInvestment;
                    existing.CurrentValue = currentValue;
                    existing.DayChange = dayChange;
                    existing.TotalReturn = totalReturn;
                    existing.XIRR = xirr;
                    snapshot = existing;
                }
                else
                {
                    snapshot = new PortfolioPerformance
                    {
                        Id = Guid.NewGuid(),
                        ProfileId = profileId,
                        Date = snapshotDateUtc,
                        TotalInvestment = totalInvestment,
                        CurrentValue = currentValue,
                        DayChange = dayChange,
                        TotalReturn = totalReturn,
                        XIRR = xirr,
                        CreatedAt = DateTime.UtcNow
                    };
                    _context.PortfolioPerformances.Add(snapshot);
                }

                await _context.SaveChangesAsync();

                return Result<PerformanceResponse>.Success(MapToResponse(snapshot), "Snapshot recorded successfully");
            }
            catch (Exception ex)
            {
                return Result<PerformanceResponse>.InternalServerError($"Error recording snapshot: {ex.Message}");
            }
        }

        public async Task<Result<PerformanceHistoryResponse>> GetPerformanceHistoryAsync(Guid userId, Guid profileId, int days = 90)
        {
            try
            {
                var profile = await _context.Profiles.AsNoTracking().FirstOrDefaultAsync(p => p.Id == profileId);
                if (profile == null)
                    return Result<PerformanceHistoryResponse>.NotFound("Profile not found");
                if (profile.UserId != userId)
                    return Result<PerformanceHistoryResponse>.Forbidden("Access denied");

                var from = DateTime.UtcNow.AddDays(-days);
                var history = await _context.PortfolioPerformances
                    .Where(pp => pp.ProfileId == profileId && pp.Date >= from)
                    .OrderBy(pp => pp.Date)
                    .Select(pp => MapToResponse(pp))
                    .ToListAsync();

                var latest = history.Count > 0 ? history[^1] : null;

                return Result<PerformanceHistoryResponse>.Success(new PerformanceHistoryResponse
                {
                    History = history,
                    Latest = latest
                }, "Performance history retrieved successfully");
            }
            catch (Exception ex)
            {
                return Result<PerformanceHistoryResponse>.InternalServerError($"Error retrieving performance history: {ex.Message}");
            }
        }

        public async Task<Result<PerformanceResponse>> GetLatestPerformanceAsync(Guid userId, Guid profileId)
        {
            try
            {
                var profile = await _context.Profiles.AsNoTracking().FirstOrDefaultAsync(p => p.Id == profileId);
                if (profile == null)
                    return Result<PerformanceResponse>.NotFound("Profile not found");
                if (profile.UserId != userId)
                    return Result<PerformanceResponse>.Forbidden("Access denied");

                var latest = await _context.PortfolioPerformances
                    .Where(pp => pp.ProfileId == profileId)
                    .OrderByDescending(pp => pp.Date)
                    .FirstOrDefaultAsync();

                if (latest == null)
                    return Result<PerformanceResponse>.NotFound("No performance snapshots found");

                return Result<PerformanceResponse>.Success(MapToResponse(latest), "Latest performance retrieved successfully");
            }
            catch (Exception ex)
            {
                return Result<PerformanceResponse>.InternalServerError($"Error retrieving latest performance: {ex.Message}");
            }
        }

        private static List<(DateTime date, decimal amount)> BuildCashFlows(
            List<Transaction> transactions, decimal currentValue, DateTime today)
        {
            var cashFlows = new List<(DateTime date, decimal amount)>();

            foreach (var tx in transactions)
            {
                if (tx.Type == TransactionType.Buy)
                    cashFlows.Add((tx.TransactionDate, -tx.Amount));
                else if (tx.Type == TransactionType.Sell || tx.Type == TransactionType.Dividend || tx.Type == TransactionType.Interest)
                    cashFlows.Add((tx.TransactionDate, tx.Amount));
            }

            if (currentValue > 0)
                cashFlows.Add((today, currentValue));

            return cashFlows;
        }

        private static PerformanceResponse MapToResponse(PortfolioPerformance pp) => new()
        {
            Id = pp.Id,
            ProfileId = pp.ProfileId,
            Date = pp.Date,
            TotalInvestment = pp.TotalInvestment,
            CurrentValue = pp.CurrentValue,
            DayChange = pp.DayChange,
            TotalReturn = pp.TotalReturn,
            XIRR = pp.XIRR,
            CreatedAt = pp.CreatedAt
        };
    }
}
