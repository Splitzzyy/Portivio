using Microsoft.EntityFrameworkCore;
using Portivio.Application.DTOs.PriceHistory;
using Portivio.Application.Results;
using Portivio.Domain.Entities;
using Portivio.Infrastructure.Data;

namespace Portivio.Application.Services
{
    public interface IPriceHistoryService
    {
        Task<Result<List<PriceHistoryResponse>>> GetPriceHistoryAsync(Guid instrumentId, DateTime? from, DateTime? to);
        Task<Result<PriceHistoryResponse>> GetLatestPriceAsync(Guid instrumentId);
        Task<Result<PriceHistoryResponse>> AddPriceAsync(Guid instrumentId, AddPriceRequest request);
        Task<Result<BulkAddPriceResponse>> BulkAddPricesAsync(Guid instrumentId, BulkAddPriceRequest request);
        Task<Result> DeletePriceAsync(Guid instrumentId, Guid priceId);
    }

    public class PriceHistoryService : IPriceHistoryService
    {
        private readonly PortivioDbContext _context;
        private readonly IHoldingService _holdingService;

        public PriceHistoryService(PortivioDbContext context, IHoldingService holdingService)
        {
            _context = context;
            _holdingService = holdingService;
        }

        public async Task<Result<List<PriceHistoryResponse>>> GetPriceHistoryAsync(Guid instrumentId, DateTime? from, DateTime? to)
        {
            try
            {
                var instrumentExists = await _context.Instruments.AnyAsync(i => i.Id == instrumentId);
                if (!instrumentExists)
                    return Result<List<PriceHistoryResponse>>.NotFound("Instrument not found");

                var query = _context.PriceHistories.Where(ph => ph.InstrumentId == instrumentId);

                if (from.HasValue)
                    query = query.Where(ph => ph.Date >= from.Value.ToUniversalTime());

                if (to.HasValue)
                    query = query.Where(ph => ph.Date <= to.Value.ToUniversalTime());

                var history = await query
                    .OrderBy(ph => ph.Date)
                    .Select(ph => MapToResponse(ph))
                    .ToListAsync();

                return Result<List<PriceHistoryResponse>>.Success(history, "Price history retrieved successfully");
            }
            catch (Exception ex)
            {
                return Result<List<PriceHistoryResponse>>.InternalServerError($"Error retrieving price history: {ex.Message}");
            }
        }

        public async Task<Result<PriceHistoryResponse>> GetLatestPriceAsync(Guid instrumentId)
        {
            try
            {
                var instrumentExists = await _context.Instruments.AnyAsync(i => i.Id == instrumentId);
                if (!instrumentExists)
                    return Result<PriceHistoryResponse>.NotFound("Instrument not found");

                var latest = await _context.PriceHistories
                    .Where(ph => ph.InstrumentId == instrumentId)
                    .OrderByDescending(ph => ph.Date)
                    .FirstOrDefaultAsync();

                if (latest == null)
                    return Result<PriceHistoryResponse>.NotFound("No price history found for this instrument");

                return Result<PriceHistoryResponse>.Success(MapToResponse(latest), "Latest price retrieved successfully");
            }
            catch (Exception ex)
            {
                return Result<PriceHistoryResponse>.InternalServerError($"Error retrieving latest price: {ex.Message}");
            }
        }

        public async Task<Result<PriceHistoryResponse>> AddPriceAsync(Guid instrumentId, AddPriceRequest request)
        {
            try
            {
                if (request.Price <= 0)
                    return Result<PriceHistoryResponse>.BadRequest("Price must be greater than zero");

                var instrumentExists = await _context.Instruments.AnyAsync(i => i.Id == instrumentId);
                if (!instrumentExists)
                    return Result<PriceHistoryResponse>.NotFound("Instrument not found");

                var normalizedDate = request.Date.ToUniversalTime().Date;
                var exists = await _context.PriceHistories.AnyAsync(
                    ph => ph.InstrumentId == instrumentId && ph.Date.Date == normalizedDate);

                if (exists)
                    return Result<PriceHistoryResponse>.Conflict("A price entry already exists for this instrument on this date");

                var priceHistory = new PriceHistory
                {
                    Id = Guid.NewGuid(),
                    InstrumentId = instrumentId,
                    Price = request.Price,
                    Date = new DateTime(normalizedDate.Year, normalizedDate.Month, normalizedDate.Day, 0, 0, 0, DateTimeKind.Utc),
                    Source = request.Source?.Trim() ?? string.Empty,
                    CreatedAt = DateTime.UtcNow
                };

                _context.PriceHistories.Add(priceHistory);
                await _context.SaveChangesAsync();

                var holdingResult = await _holdingService.UpdateCurrentPriceAsync(instrumentId, request.Price);
                if (holdingResult.IsFailure)
                    return Result<PriceHistoryResponse>.InternalServerError($"Price saved but holding update failed: {holdingResult.Message}");

                return Result<PriceHistoryResponse>.Success(MapToResponse(priceHistory), "Price added successfully", 201);
            }
            catch (Exception ex)
            {
                return Result<PriceHistoryResponse>.InternalServerError($"Error adding price: {ex.Message}");
            }
        }

        public async Task<Result<BulkAddPriceResponse>> BulkAddPricesAsync(Guid instrumentId, BulkAddPriceRequest request)
        {
            try
            {
                var instrumentExists = await _context.Instruments.AnyAsync(i => i.Id == instrumentId);
                if (!instrumentExists)
                    return Result<BulkAddPriceResponse>.NotFound("Instrument not found");

                var response = new BulkAddPriceResponse();

                // Extract all dates and normalize them
                var dates = request.Prices.Select(p => p.Date.ToUniversalTime().Date).Distinct().ToList();

                // Fetch existing entries for these dates in one query
                var existingDates = await _context.PriceHistories
                    .Where(ph => ph.InstrumentId == instrumentId && dates.Contains(ph.Date.Date))
                    .Select(ph => ph.Date.Date)
                    .ToListAsync();

                var existingDateSet = new HashSet<DateTime>(existingDates);

                foreach (var item in request.Prices)
                {
                    if (item.Price <= 0)
                    {
                        response.Errors.Add($"Skipped entry for {item.Date:yyyy-MM-dd}: price must be > 0");
                        response.Skipped++;
                        continue;
                    }

                    var normalizedDate = item.Date.ToUniversalTime().Date;
                    if (existingDateSet.Contains(normalizedDate))
                    {
                        response.Skipped++;
                        continue;
                    }

                    _context.PriceHistories.Add(new PriceHistory
                    {
                        Id = Guid.NewGuid(),
                        InstrumentId = instrumentId,
                        Price = item.Price,
                        Date = new DateTime(normalizedDate.Year, normalizedDate.Month, normalizedDate.Day, 0, 0, 0, DateTimeKind.Utc),
                        Source = item.Source?.Trim() ?? string.Empty,
                        CreatedAt = DateTime.UtcNow
                    });

                    response.Inserted++;
                    existingDateSet.Add(normalizedDate); // Avoid duplicates within the same batch
                }

                await _context.SaveChangesAsync();

                if (response.Inserted > 0)
                {
                    var actualLatest = await _context.PriceHistories
                        .Where(ph => ph.InstrumentId == instrumentId)
                        .OrderByDescending(ph => ph.Date)
                        .Select(ph => (decimal?)ph.Price)
                        .FirstOrDefaultAsync();

                    if (actualLatest.HasValue)
                    {
                        var holdingResult = await _holdingService.UpdateCurrentPriceAsync(instrumentId, actualLatest.Value);
                        if (holdingResult.IsFailure)
                            return Result<BulkAddPriceResponse>.InternalServerError($"Prices saved but holding update failed: {holdingResult.Message}");
                    }
                }

                return Result<BulkAddPriceResponse>.Success(response, $"Bulk import complete: {response.Inserted} inserted, {response.Skipped} skipped");
            }
            catch (Exception ex)
            {
                return Result<BulkAddPriceResponse>.InternalServerError($"Error bulk adding prices: {ex.Message}");
            }
        }

        public async Task<Result> DeletePriceAsync(Guid instrumentId, Guid priceId)
        {
            try
            {
                var price = await _context.PriceHistories
                    .FirstOrDefaultAsync(ph => ph.Id == priceId && ph.InstrumentId == instrumentId);

                if (price == null)
                    return Result.NotFound("Price entry not found");

                _context.PriceHistories.Remove(price);
                await _context.SaveChangesAsync();

                var newLatest = await _context.PriceHistories
                    .Where(ph => ph.InstrumentId == instrumentId)
                    .OrderByDescending(ph => ph.Date)
                    .Select(ph => (decimal?)ph.Price)
                    .FirstOrDefaultAsync();

                if (newLatest.HasValue)
                {
                    var holdingResult = await _holdingService.UpdateCurrentPriceAsync(instrumentId, newLatest.Value);
                    if (holdingResult.IsFailure)
                        return Result.InternalServerError($"Price deleted but holding update failed: {holdingResult.Message}");
                }

                return Result.Success("Price entry deleted successfully");
            }
            catch (Exception ex)
            {
                return Result.InternalServerError($"Error deleting price: {ex.Message}");
            }
        }

        private static PriceHistoryResponse MapToResponse(PriceHistory ph) => new()
        {
            Id = ph.Id,
            InstrumentId = ph.InstrumentId,
            Price = ph.Price,
            Date = ph.Date,
            Source = ph.Source,
            CreatedAt = ph.CreatedAt
        };
    }
}
