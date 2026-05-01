using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Portivio.Application.DTOs.Instrument;
using Portivio.Application.Results;
using Portivio.Domain.Entities;
using Portivio.Infrastructure.Data;

namespace Portivio.Application.Services
{
    public interface IInstrumentService
    {
        Task<Result<List<AssetTypeResponse>>> GetAssetTypesAsync();
        Task<Result<AssetTypeResponse>> CreateAssetTypeAsync(CreateAssetTypeRequest request);
        Task<Result> DeleteAssetTypeAsync(Guid assetTypeId);

        Task<Result<List<InstrumentResponse>>> GetInstrumentsAsync(Guid? assetTypeId = null);
        Task<Result<InstrumentResponse>> GetInstrumentAsync(Guid instrumentId);
        Task<Result<InstrumentResponse>> CreateInstrumentAsync(CreateInstrumentRequest request);
        Task<Result<InstrumentResponse>> UpdateInstrumentAsync(Guid instrumentId, UpdateInstrumentRequest request);
        Task<Result> DeleteInstrumentAsync(Guid instrumentId);
    }

    public class InstrumentService : IInstrumentService
    {
        private readonly PortivioDbContext _context;
        private readonly ILogger<InstrumentService> _logger;

        public InstrumentService(PortivioDbContext context, ILogger<InstrumentService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<Result<List<AssetTypeResponse>>> GetAssetTypesAsync()
        {
            try
            {
                var assetTypes = await _context.AssetTypes
                    .OrderBy(a => a.Name)
                    .Select(a => new AssetTypeResponse { Id = a.Id, Name = a.Name })
                    .ToListAsync();

                return Result<List<AssetTypeResponse>>.Success(assetTypes, "Asset types retrieved successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving asset types");
                return Result<List<AssetTypeResponse>>.InternalServerError($"Error retrieving asset types: {ex.Message}");
            }
        }

        public async Task<Result<AssetTypeResponse>> CreateAssetTypeAsync(CreateAssetTypeRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.Name))
                    return Result<AssetTypeResponse>.BadRequest("Asset type name is required");

                var exists = await _context.AssetTypes
                    .AnyAsync(a => a.Name.ToLower() == request.Name.ToLower());

                if (exists)
                {
                    _logger.LogWarning("Asset type creation rejected: duplicate name. Name={Name}", request.Name);
                    return Result<AssetTypeResponse>.Conflict("Asset type with this name already exists");
                }

                var assetType = new AssetType
                {
                    Id = Guid.NewGuid(),
                    Name = request.Name.Trim()
                };

                _context.AssetTypes.Add(assetType);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Asset type created. AssetTypeId={AssetTypeId} Name={Name}", assetType.Id, assetType.Name);

                return Result<AssetTypeResponse>.Success(
                    new AssetTypeResponse { Id = assetType.Id, Name = assetType.Name },
                    "Asset type created successfully", 201);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating asset type. Name={Name}", request.Name);
                return Result<AssetTypeResponse>.InternalServerError($"Error creating asset type: {ex.Message}");
            }
        }

        public async Task<Result> DeleteAssetTypeAsync(Guid assetTypeId)
        {
            try
            {
                var assetType = await _context.AssetTypes.FirstOrDefaultAsync(a => a.Id == assetTypeId);
                if (assetType == null)
                {
                    _logger.LogWarning("Asset type delete rejected: not found. AssetTypeId={AssetTypeId}", assetTypeId);
                    return Result.NotFound("Asset type not found");
                }

                var hasInstruments = await _context.Instruments.AnyAsync(i => i.AssetTypeId == assetTypeId);
                if (hasInstruments)
                {
                    _logger.LogWarning("Asset type delete rejected: instruments exist. AssetTypeId={AssetTypeId}", assetTypeId);
                    return Result.Conflict("Asset type has associated instruments. Remove them first.");
                }

                _context.AssetTypes.Remove(assetType);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Asset type deleted. AssetTypeId={AssetTypeId}", assetTypeId);

                return Result.Success("Asset type deleted successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting asset type. AssetTypeId={AssetTypeId}", assetTypeId);
                return Result.InternalServerError($"Error deleting asset type: {ex.Message}");
            }
        }

        public async Task<Result<List<InstrumentResponse>>> GetInstrumentsAsync(Guid? assetTypeId = null)
        {
            try
            {
                var query = _context.Instruments.Include(i => i.AssetType).AsQueryable();

                if (assetTypeId.HasValue)
                    query = query.Where(i => i.AssetTypeId == assetTypeId.Value);

                var instruments = await query
                    .OrderBy(i => i.Name)
                    .Select(i => new InstrumentResponse
                    {
                        Id = i.Id,
                        AssetTypeId = i.AssetTypeId,
                        AssetTypeName = i.AssetType.Name,
                        Name = i.Name,
                        Symbol = i.Symbol,
                        Currency = i.Currency
                    })
                    .ToListAsync();

                return Result<List<InstrumentResponse>>.Success(instruments, "Instruments retrieved successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving instruments. AssetTypeId={AssetTypeId}", assetTypeId);
                return Result<List<InstrumentResponse>>.InternalServerError($"Error retrieving instruments: {ex.Message}");
            }
        }

        public async Task<Result<InstrumentResponse>> GetInstrumentAsync(Guid instrumentId)
        {
            try
            {
                var instrument = await _context.Instruments
                    .Include(i => i.AssetType)
                    .FirstOrDefaultAsync(i => i.Id == instrumentId);

                if (instrument == null)
                {
                    _logger.LogWarning("Instrument lookup rejected: not found. InstrumentId={InstrumentId}", instrumentId);
                    return Result<InstrumentResponse>.NotFound("Instrument not found");
                }

                return Result<InstrumentResponse>.Success(new InstrumentResponse
                {
                    Id = instrument.Id,
                    AssetTypeId = instrument.AssetTypeId,
                    AssetTypeName = instrument.AssetType.Name,
                    Name = instrument.Name,
                    Symbol = instrument.Symbol,
                    Currency = instrument.Currency
                }, "Instrument retrieved successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving instrument. InstrumentId={InstrumentId}", instrumentId);
                return Result<InstrumentResponse>.InternalServerError($"Error retrieving instrument: {ex.Message}");
            }
        }

        public async Task<Result<InstrumentResponse>> CreateInstrumentAsync(CreateInstrumentRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.Name))
                    return Result<InstrumentResponse>.BadRequest("Instrument name is required");

                if (string.IsNullOrWhiteSpace(request.Symbol))
                    return Result<InstrumentResponse>.BadRequest("Instrument symbol is required");

                if (string.IsNullOrWhiteSpace(request.Currency))
                    return Result<InstrumentResponse>.BadRequest("Currency is required");

                var assetType = await _context.AssetTypes.FirstOrDefaultAsync(a => a.Id == request.AssetTypeId);
                if (assetType == null)
                {
                    _logger.LogWarning("Instrument creation rejected: asset type not found. AssetTypeId={AssetTypeId}", request.AssetTypeId);
                    return Result<InstrumentResponse>.BadRequest("Asset type not found");
                }

                var symbolExists = await _context.Instruments
                    .AnyAsync(i => i.Symbol.ToLower() == request.Symbol.ToLower());

                if (symbolExists)
                {
                    _logger.LogWarning("Instrument creation rejected: duplicate symbol. Symbol={Symbol}", request.Symbol);
                    return Result<InstrumentResponse>.Conflict("Instrument with this symbol already exists");
                }

                var instrument = new Instrument
                {
                    Id = Guid.NewGuid(),
                    AssetTypeId = request.AssetTypeId,
                    Name = request.Name.Trim(),
                    Symbol = request.Symbol.ToUpperInvariant(),
                    Currency = request.Currency.ToUpperInvariant()
                };

                _context.Instruments.Add(instrument);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Instrument created. InstrumentId={InstrumentId} Symbol={Symbol} AssetTypeId={AssetTypeId}",
                    instrument.Id, instrument.Symbol, instrument.AssetTypeId);

                return Result<InstrumentResponse>.Success(new InstrumentResponse
                {
                    Id = instrument.Id,
                    AssetTypeId = instrument.AssetTypeId,
                    AssetTypeName = assetType.Name,
                    Name = instrument.Name,
                    Symbol = instrument.Symbol,
                    Currency = instrument.Currency
                }, "Instrument created successfully", 201);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating instrument. Symbol={Symbol}", request.Symbol);
                return Result<InstrumentResponse>.InternalServerError($"Error creating instrument: {ex.Message}");
            }
        }

        public async Task<Result<InstrumentResponse>> UpdateInstrumentAsync(Guid instrumentId, UpdateInstrumentRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.Name))
                    return Result<InstrumentResponse>.BadRequest("Instrument name is required");

                if (string.IsNullOrWhiteSpace(request.Symbol))
                    return Result<InstrumentResponse>.BadRequest("Instrument symbol is required");

                if (string.IsNullOrWhiteSpace(request.Currency))
                    return Result<InstrumentResponse>.BadRequest("Currency is required");

                var instrument = await _context.Instruments
                    .Include(i => i.AssetType)
                    .FirstOrDefaultAsync(i => i.Id == instrumentId);

                if (instrument == null)
                {
                    _logger.LogWarning("Instrument update rejected: not found. InstrumentId={InstrumentId}", instrumentId);
                    return Result<InstrumentResponse>.NotFound("Instrument not found");
                }

                var symbolExists = await _context.Instruments
                    .AnyAsync(i => i.Symbol.ToLower() == request.Symbol.ToLower() && i.Id != instrumentId);

                if (symbolExists)
                {
                    _logger.LogWarning("Instrument update rejected: duplicate symbol. InstrumentId={InstrumentId} Symbol={Symbol}",
                        instrumentId, request.Symbol);
                    return Result<InstrumentResponse>.Conflict("Instrument with this symbol already exists");
                }

                instrument.Name = request.Name.Trim();
                instrument.Symbol = request.Symbol.ToUpperInvariant();
                instrument.Currency = request.Currency.ToUpperInvariant();

                await _context.SaveChangesAsync();

                _logger.LogInformation("Instrument updated. InstrumentId={InstrumentId} Symbol={Symbol}", instrumentId, instrument.Symbol);

                return Result<InstrumentResponse>.Success(new InstrumentResponse
                {
                    Id = instrument.Id,
                    AssetTypeId = instrument.AssetTypeId,
                    AssetTypeName = instrument.AssetType.Name,
                    Name = instrument.Name,
                    Symbol = instrument.Symbol,
                    Currency = instrument.Currency
                }, "Instrument updated successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating instrument. InstrumentId={InstrumentId}", instrumentId);
                return Result<InstrumentResponse>.InternalServerError($"Error updating instrument: {ex.Message}");
            }
        }

        public async Task<Result> DeleteInstrumentAsync(Guid instrumentId)
        {
            try
            {
                var instrument = await _context.Instruments.FirstOrDefaultAsync(i => i.Id == instrumentId);
                if (instrument == null)
                {
                    _logger.LogWarning("Instrument delete rejected: not found. InstrumentId={InstrumentId}", instrumentId);
                    return Result.NotFound("Instrument not found");
                }

                var hasHoldings = await _context.Holdings.AnyAsync(h => h.InstrumentId == instrumentId);
                if (hasHoldings)
                {
                    _logger.LogWarning("Instrument delete rejected: holdings exist. InstrumentId={InstrumentId}", instrumentId);
                    return Result.Conflict("Instrument has associated holdings. Remove them first.");
                }

                var hasTransactions = await _context.Transactions.AnyAsync(t => t.InstrumentId == instrumentId);
                if (hasTransactions)
                {
                    _logger.LogWarning("Instrument delete rejected: transactions exist. InstrumentId={InstrumentId}", instrumentId);
                    return Result.Conflict("Instrument has associated transactions. Remove them first.");
                }

                _context.Instruments.Remove(instrument);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Instrument deleted. InstrumentId={InstrumentId}", instrumentId);

                return Result.Success("Instrument deleted successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting instrument. InstrumentId={InstrumentId}", instrumentId);
                return Result.InternalServerError($"Error deleting instrument: {ex.Message}");
            }
        }
    }
}
