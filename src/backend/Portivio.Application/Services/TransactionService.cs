using Microsoft.EntityFrameworkCore;
using Portivio.Application.DTOs.Transaction;
using Portivio.Application.Results;
using Portivio.Application.Services.Authorization;
using Portivio.Domain.Entities;
using Portivio.Domain.Enums;
using Portivio.Infrastructure.Data;
using System.Net;

namespace Portivio.Application.Services
{
    public interface ITransactionService
    {
        Task<Result<PagedResult<TransactionResponse>>> GetTransactionsAsync(Guid userId, Guid profileId, int page = 1, int pageSize = 50, bool includeDeleted = false);
        Task<Result<TransactionResponse>> CreateTransactionAsync(Guid userId, Guid profileId, CreateTransactionRequest request);
        Task<Result<TransactionResponse>> UpdateTransactionAsync(Guid userId, Guid profileId, Guid txId, UpdateTransactionRequest request);
        Task<Result> DeleteTransactionAsync(Guid userId, Guid profileId, Guid txId);
    }

    public class TransactionService : ITransactionService
    {
        private readonly PortivioDbContext _context;
        private readonly IHoldingService _holdingService;
        private readonly IProfileAccessGuard _profileAccess;

        public TransactionService(PortivioDbContext context, IHoldingService holdingService, IProfileAccessGuard profileAccess)
        {
            _context = context;
            _holdingService = holdingService;
            _profileAccess = profileAccess;
        }

        private async Task<Result<T>> ExecuteInTransactionAsync<T>(Func<Task<Result<T>>> action)
        {
            // Skip creating a nested transaction if one is already active (e.g. TransactionFilter).
            if (!_context.Database.IsRelational() || _context.Database.CurrentTransaction is not null)
                return await action();

            await using var dbTx = await _context.Database.BeginTransactionAsync();
            try
            {
                var result = await action();
                if (result.IsFailure)
                {
                    await dbTx.RollbackAsync();
                    return result;
                }
                await dbTx.CommitAsync();
                return result;
            }
            catch
            {
                await dbTx.RollbackAsync();
                throw;
            }
        }

        private async Task<Result> ExecuteInTransactionAsync(Func<Task<Result>> action)
        {
            // Skip creating a nested transaction if one is already active (e.g. TransactionFilter).
            if (!_context.Database.IsRelational() || _context.Database.CurrentTransaction is not null)
                return await action();

            await using var dbTx = await _context.Database.BeginTransactionAsync();
            try
            {
                var result = await action();
                if (result.IsFailure)
                {
                    await dbTx.RollbackAsync();
                    return result;
                }
                await dbTx.CommitAsync();
                return result;
            }
            catch
            {
                await dbTx.RollbackAsync();
                throw;
            }
        }

        public async Task<Result<PagedResult<TransactionResponse>>> GetTransactionsAsync(Guid userId, Guid profileId, int page = 1, int pageSize = 50, bool includeDeleted = false)
        {
            try
            {
                if (page < 1) page = 1;
                if (pageSize < 1) pageSize = 1;
                if (pageSize > 200) pageSize = 200;

                var access = await _profileAccess.EnsureOwnerAsync(userId, profileId);
                if (access.IsFailure)
                    return access.ToFailure<PagedResult<TransactionResponse>>();

                var baseQuery = includeDeleted
                    ? _context.Transactions.IgnoreQueryFilters().Where(t => t.ProfileId == profileId)
                    : _context.Transactions.Where(t => t.ProfileId == profileId);

                var total = await baseQuery.CountAsync();

                var skip = (page - 1) * pageSize;
                var transactions = await baseQuery
                    .Include(t => t.Instrument)
                    .OrderByDescending(t => t.TransactionDate)
                    .ThenByDescending(t => t.CreatedAtUtc)
                    .Skip(skip)
                    .Take(pageSize)
                    .Select(t => MapToResponse(t))
                    .ToListAsync();

                var paged = PagedResult<TransactionResponse>.Create(transactions, page, pageSize, total);
                return Result<PagedResult<TransactionResponse>>.Success(paged, "Transactions retrieved successfully");
            }
            catch (Exception ex)
            {
                return Result<PagedResult<TransactionResponse>>.InternalServerError($"Error retrieving transactions: {ex.Message}");
            }
        }

        public async Task<Result<TransactionResponse>> CreateTransactionAsync(Guid userId, Guid profileId, CreateTransactionRequest request)
        {
            var access = await _profileAccess.EnsureOwnerAsync(userId, profileId);
            if (access.IsFailure)
                return access.ToFailure<TransactionResponse>();

            if (!Enum.IsDefined(typeof(TransactionType), request.Type))
                return Result<TransactionResponse>.BadRequest("Invalid transaction type");

            var txType = request.Type;

            var instrument = await _context.Instruments.FirstOrDefaultAsync(i => i.Id == request.InstrumentId);
            if (instrument == null)
                return Result<TransactionResponse>.BadRequest("Instrument not found");

            if (txType == TransactionType.Buy || txType == TransactionType.Sell)
            {
                if (request.Quantity <= 0)
                    return Result<TransactionResponse>.BadRequest("Quantity must be greater than zero for Buy/Sell transactions");
                if (request.Price <= 0)
                    return Result<TransactionResponse>.BadRequest("Price must be greater than zero for Buy/Sell transactions");
            }
            else
            {
                if (request.Amount <= 0)
                    return Result<TransactionResponse>.BadRequest("Amount must be greater than zero for Dividend/Interest transactions");
            }

            var amount = request.Amount > 0 ? request.Amount : request.Quantity * request.Price;

            var now = DateTime.UtcNow;
            var transaction = new Transaction
            {
                Id = Guid.NewGuid(),
                ProfileId = profileId,
                InstrumentId = request.InstrumentId,
                Type = txType,
                Quantity = request.Quantity,
                Price = request.Price,
                Amount = amount,
                TransactionDate = request.TransactionDate.Kind == DateTimeKind.Utc
                    ? request.TransactionDate
                    : request.TransactionDate.ToUniversalTime(),
                Notes = request.Notes?.Trim() ?? string.Empty,
                Source = TransactionSource.Manual,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };

            return await ExecuteInTransactionAsync(async () =>
            {
                try
                {
                    _context.Transactions.Add(transaction);
                    await _context.SaveChangesAsync();

                    var holdingResult = await _holdingService.RecalculateHoldingFromTransactionsAsync(profileId, request.InstrumentId);
                    if (holdingResult.IsFailure)
                        return Result<TransactionResponse>.InternalServerError($"Holding recalculation failed: {holdingResult.Message}");

                    return Result<TransactionResponse>.Success(new TransactionResponse
                    {
                        Id = transaction.Id,
                        ProfileId = transaction.ProfileId,
                        InstrumentId = transaction.InstrumentId,
                        InstrumentName = instrument.Name,
                        InstrumentSymbol = instrument.Symbol,
                        Type = transaction.Type,
                        Quantity = transaction.Quantity,
                        Price = transaction.Price,
                        Amount = transaction.Amount,
                        TransactionDate = transaction.TransactionDate,
                        Notes = transaction.Notes
                    }, "Transaction created successfully", 201);
                }
                catch (Exception ex)
                {
                    return Result<TransactionResponse>.InternalServerError($"Error creating transaction: {ex.Message}");
                }
            });
        }

        public async Task<Result<TransactionResponse>> UpdateTransactionAsync(Guid userId, Guid profileId, Guid txId, UpdateTransactionRequest request)
        {
            var access = await _profileAccess.EnsureOwnerAsync(userId, profileId);
            if (access.IsFailure)
                return access.ToFailure<TransactionResponse>();

            var transaction = await _context.Transactions
                .Include(t => t.Instrument)
                .FirstOrDefaultAsync(t => t.Id == txId && t.ProfileId == profileId);

            if (transaction == null)
                return Result<TransactionResponse>.NotFound("Transaction not found");

            transaction.Quantity = request.Quantity;
            transaction.Price = request.Price;
            transaction.Amount = request.Amount > 0 ? request.Amount : request.Quantity * request.Price;
            transaction.TransactionDate = request.TransactionDate.Kind == DateTimeKind.Utc
                ? request.TransactionDate
                : request.TransactionDate.ToUniversalTime();
            transaction.Notes = request.Notes?.Trim() ?? string.Empty;
            transaction.UpdatedAtUtc = DateTime.UtcNow;

            return await ExecuteInTransactionAsync(async () =>
            {
                try
                {
                    await _context.SaveChangesAsync();

                    var holdingResult = await _holdingService.RecalculateHoldingFromTransactionsAsync(profileId, transaction.InstrumentId);
                    if (holdingResult.IsFailure)
                        return Result<TransactionResponse>.InternalServerError($"Holding recalculation failed: {holdingResult.Message}");

                    return Result<TransactionResponse>.Success(MapToResponse(transaction), "Transaction updated successfully");
                }
                catch (Exception ex)
                {
                    return Result<TransactionResponse>.InternalServerError($"Error updating transaction: {ex.Message}");
                }
            });
        }

        public async Task<Result> DeleteTransactionAsync(Guid userId, Guid profileId, Guid txId)
        {
            var access = await _profileAccess.EnsureOwnerAsync(userId, profileId);
            if (access.IsFailure)
                return access.ToFailure();

            var transaction = await _context.Transactions
                .FirstOrDefaultAsync(t => t.Id == txId && t.ProfileId == profileId);

            if (transaction == null)
                return Result.NotFound("Transaction not found");

            var instrumentId = transaction.InstrumentId;

            return await ExecuteInTransactionAsync(async () =>
            {
                try
                {
                    transaction.IsDeleted = true;
                    transaction.UpdatedAtUtc = DateTime.UtcNow;
                    await _context.SaveChangesAsync();

                    var holdingResult = await _holdingService.RecalculateHoldingFromTransactionsAsync(profileId, instrumentId);
                    if (holdingResult.IsFailure)
                        return Result.InternalServerError($"Holding recalculation failed: {holdingResult.Message}");

                    return Result.Success("Transaction deleted successfully");
                }
                catch (Exception ex)
                {
                    return Result.InternalServerError($"Error deleting transaction: {ex.Message}");
                }
            });
        }

        private static TransactionResponse MapToResponse(Transaction t) => new()
        {
            Id = t.Id,
            ProfileId = t.ProfileId,
            InstrumentId = t.InstrumentId,
            InstrumentName = t.Instrument.Name,
            InstrumentSymbol = t.Instrument.Symbol,
            Type = t.Type,
            Quantity = t.Quantity,
            Price = t.Price,
            Amount = t.Amount,
            TransactionDate = t.TransactionDate,
            Notes = t.Notes,
            IsDeleted = t.IsDeleted
        };
    }
}
