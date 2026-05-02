using Microsoft.EntityFrameworkCore;
using Portivio.Application.DTOs.Transaction;
using Portivio.Application.Results;
using Portivio.Application.Services.Authorization;
using Portivio.Domain.Entities;
using Portivio.Domain.Enums;
using Portivio.Infrastructure.Data;

namespace Portivio.Application.Services
{
    public interface ITransactionService
    {
        Task<Result<List<TransactionResponse>>> GetTransactionsAsync(Guid userId, Guid profileId, int page = 1, int pageSize = 50);
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
            if (!_context.Database.IsRelational())
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
            if (!_context.Database.IsRelational())
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

        public async Task<Result<List<TransactionResponse>>> GetTransactionsAsync(Guid userId, Guid profileId, int page = 1, int pageSize = 50)
        {
            try
            {
                var access = await _profileAccess.EnsureOwnerAsync(userId, profileId);
                if (access.IsFailure)
                    return access.ToFailure<List<TransactionResponse>>();

                var skip = (page - 1) * pageSize;
                var transactions = await _context.Transactions
                    .Include(t => t.Instrument)
                    .Where(t => t.ProfileId == profileId)
                    .OrderByDescending(t => t.TransactionDate)
                    .Skip(skip)
                    .Take(pageSize)
                    .Select(t => MapToResponse(t))
                    .ToListAsync();

                return Result<List<TransactionResponse>>.Success(transactions, "Transactions retrieved successfully");
            }
            catch (Exception ex)
            {
                return Result<List<TransactionResponse>>.InternalServerError($"Error retrieving transactions: {ex.Message}");
            }
        }

        public async Task<Result<TransactionResponse>> CreateTransactionAsync(Guid userId, Guid profileId, CreateTransactionRequest request)
        {
            var access = await _profileAccess.EnsureOwnerAsync(userId, profileId);
            if (access.IsFailure)
                return access.ToFailure<TransactionResponse>();

            if (!Enum.TryParse<TransactionType>(request.Type, ignoreCase: true, out var txType))
                return Result<TransactionResponse>.BadRequest("Invalid transaction type. Valid values: Buy, Sell, Dividend, Interest");

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
                Notes = request.Notes?.Trim() ?? string.Empty
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
                        Type = transaction.Type.ToString(),
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
                    _context.Transactions.Remove(transaction);
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
            Type = t.Type.ToString(),
            Quantity = t.Quantity,
            Price = t.Price,
            Amount = t.Amount,
            TransactionDate = t.TransactionDate,
            Notes = t.Notes
        };
    }
}
