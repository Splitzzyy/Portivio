using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Portivio.Application.DTOs.Home;
using Portivio.Application.Results;
using Portivio.Infrastructure.Data;

namespace Portivio.Application.Services
{
    public interface IHomeService
    {
        Task<Result<HomeResponse>> GetHomeDataAsync(Guid userId);
    }

    public class HomeService : IHomeService
    {
        private readonly PortivioDbContext _context;
        private readonly ILogger<HomeService> _logger;

        public HomeService(PortivioDbContext context, ILogger<HomeService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<Result<HomeResponse>> GetHomeDataAsync(Guid userId)
        {
            try
            {
                if (userId == Guid.Empty)
                    return Result<HomeResponse>.BadRequest("User id is required");

                var user = await _context.Users
                    .AsNoTracking()
                    .FirstOrDefaultAsync(u => u.Id == userId);

                if (user == null)
                {
                    _logger.LogWarning("Home data lookup rejected: user not found. UserId={UserId}", userId);
                    return Result<HomeResponse>.NotFound("User not found");
                }

                var profiles = await _context.Profiles
                    .AsNoTracking()
                    .Where(p => p.UserId == userId)
                    .Include(p => p.Holdings).ThenInclude(h => h.Instrument).ThenInclude(i => i.AssetType)
                    .Include(p => p.Transactions).ThenInclude(t => t.Instrument)
                    .Include(p => p.SIPPlans).ThenInclude(s => s.Instrument)
                    .Include(p => p.PortfolioPerformances)
                    .AsSplitQuery()
                    .ToListAsync();

                var profileDtos = profiles.Select(p => new ProfileDto
                {
                    Id = p.Id,
                    Name = p.Name,
                    BaseCurrency = p.BaseCurrency,
                    Description = p.Description,
                    CreatedAt = p.CreatedAt,
                    Holdings = p.Holdings.Select(h => new HoldingDto
                    {
                        Id = h.Id,
                        InstrumentId = h.InstrumentId,
                        InstrumentName = h.Instrument.Name,
                        InstrumentSymbol = h.Instrument.Symbol,
                        Currency = h.Instrument.Currency,
                        AssetType = h.Instrument.AssetType.Name,
                        Quantity = h.Quantity,
                        AvgPrice = h.AvgPrice,
                        CurrentPrice = h.CurrentPrice,
                        MarketValue = h.MarketValue,
                        UnrealizedPnL = h.UnrealizedPnL,
                        LastUpdated = h.LastUpdated
                    }).ToList(),
                    Transactions = p.Transactions
                        .OrderByDescending(t => t.TransactionDate)
                        .Select(t => new TransactionDto
                        {
                            Id = t.Id,
                            InstrumentId = t.InstrumentId,
                            InstrumentSymbol = t.Instrument.Symbol,
                            Type = t.Type.ToString(),
                            Quantity = t.Quantity,
                            Price = t.Price,
                            Amount = t.Amount,
                            TransactionDate = t.TransactionDate,
                            Notes = t.Notes
                        }).ToList(),
                    SIPPlans = p.SIPPlans.Select(s => new SIPPlanDto
                    {
                        Id = s.Id,
                        InstrumentId = s.InstrumentId,
                        InstrumentSymbol = s.Instrument.Symbol,
                        Amount = s.Amount,
                        SIPDay = s.SIPDay,
                        StartDate = s.StartDate,
                        EndDate = s.EndDate,
                        IsActive = s.IsActive,
                        CreatedAt = s.CreatedAt
                    }).ToList(),
                    LatestPerformance = p.PortfolioPerformances
                        .OrderByDescending(pp => pp.Date)
                        .Select(pp => new PortfolioPerformanceDto
                        {
                            Date = pp.Date,
                            TotalInvestment = pp.TotalInvestment,
                            CurrentValue = pp.CurrentValue,
                            DayChange = pp.DayChange,
                            TotalReturn = pp.TotalReturn,
                            XIRR = pp.XIRR
                        })
                        .FirstOrDefault()
                }).ToList();

                var allHoldings = profileDtos.SelectMany(p => p.Holdings).ToList();

                var summary = new PortfolioSummaryDto
                {
                    ProfileCount = profileDtos.Count,
                    HoldingCount = allHoldings.Count,
                    TransactionCount = profileDtos.Sum(p => p.Transactions.Count),
                    ActiveSIPCount = profileDtos.SelectMany(p => p.SIPPlans).Count(s => s.IsActive),
                    TotalInvestment = allHoldings.Sum(h => h.Quantity * h.AvgPrice),
                    TotalMarketValue = allHoldings.Sum(h => h.MarketValue),
                    TotalUnrealizedPnL = allHoldings.Sum(h => h.UnrealizedPnL)
                };

                var response = new HomeResponse
                {
                    User = new UserInfoDto
                    {
                        Id = user.Id,
                        Email = user.Email,
                        Name = user.Name,
                        IsVerified = user.IsVerified,
                        IsActive = user.IsActive,
                        CreatedAt = user.CreatedAt,
                        LastLoginAt = user.LastLoginAt
                    },
                    Summary = summary,
                    Profiles = profileDtos
                };

                return Result<HomeResponse>.Success(response, "Home data retrieved successfully", 200);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load home data. UserId={UserId}", userId);
                return Result<HomeResponse>.InternalServerError($"Failed to load home data: {ex.Message}");
            }
        }
    }
}
