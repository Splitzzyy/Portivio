using Microsoft.EntityFrameworkCore;
using Portivio.Application.DTOs.Transaction;
using Portivio.Application.Results;
using Portivio.Application.Services.Authorization;
using Portivio.Application.Services.Strategies;
using Portivio.Domain.Entities;
using Portivio.Domain.Enums;
using Portivio.Infrastructure.Data;

namespace Portivio.Application.Services
{
    public sealed record TransactionCommand(
        Guid ProfileId,
        Guid InstrumentId,
        TransactionType Type,
        decimal Quantity,
        decimal Price,
        decimal Amount,
        DateTime TransactionDateUtc,
        string? Notes,
        string? ClientTxnId);

    public interface ITransactionIngestService
    {
        Task<Result<TransactionResponse>> IngestAsync(
            Guid userId, TransactionCommand cmd, TransactionSource source, CancellationToken ct = default);
    }

    public class TransactionIngestService : ITransactionIngestService
    {
        private readonly PortivioDbContext _context;
        private readonly IProfileAccessGuard _profileAccess;
        private readonly AssetStrategyResolver _strategies;

        public TransactionIngestService(
            PortivioDbContext context,
            IProfileAccessGuard profileAccess,
            AssetStrategyResolver strategies)
        {
            _context = context;
            _profileAccess = profileAccess;
            _strategies = strategies;
        }

        public async Task<Result<TransactionResponse>> IngestAsync(
            Guid userId, TransactionCommand cmd, TransactionSource source, CancellationToken ct = default)
        {
            var access = await _profileAccess.EnsureOwnerAsync(userId, cmd.ProfileId, ct);
            if (access.IsFailure)
                return access.ToFailure<TransactionResponse>();

            var instrument = await _context.Instruments
                .AsNoTracking()
                .FirstOrDefaultAsync(i => i.Id == cmd.InstrumentId, ct);
            if (instrument == null)
                return Result<TransactionResponse>.BadRequest("Instrument not found");

            var strategy = _strategies.For(instrument.Category);

            var txForValidation = new Transaction
            {
                Type = cmd.Type,
                Quantity = cmd.Quantity,
                Price = cmd.Price,
                Amount = cmd.Amount > 0 ? cmd.Amount : cmd.Quantity * cmd.Price
            };
            var validationResult = strategy.ValidateTransaction(txForValidation, instrument);
            if (validationResult.IsFailure)
                return Result<TransactionResponse>.BadRequest(validationResult.Message!);

            // Idempotency probe — handled by 2.6 (stub for unique constraint in DB)
            if (cmd.ClientTxnId != null)
            {
                var existing = await _context.Transactions
                    .Include(t => t.Instrument)
                    .FirstOrDefaultAsync(t => t.ProfileId == cmd.ProfileId && t.ClientTxnId == cmd.ClientTxnId, ct);
                if (existing != null)
                    return Result<TransactionResponse>.Success(MapToResponse(existing), "Transaction already processed (idempotent)", 200);
            }

            if (!_context.Database.IsRelational())
                return await ExecuteCoreAsync(cmd, source, instrument, strategy, ct);

            await using var dbTx = await _context.Database.BeginTransactionAsync(ct);
            try
            {
                var result = await ExecuteCoreAsync(cmd, source, instrument, strategy, ct);
                if (result.IsFailure)
                {
                    await dbTx.RollbackAsync(ct);
                    return result;
                }
                await dbTx.CommitAsync(ct);
                return result;
            }
            catch
            {
                await dbTx.RollbackAsync(ct);
                throw;
            }
        }

        private async Task<Result<TransactionResponse>> ExecuteCoreAsync(
            TransactionCommand cmd,
            TransactionSource source,
            Domain.Entities.Instrument instrument,
            IAssetStrategy strategy,
            CancellationToken ct)
        {
            try
            {
                var now = DateTime.UtcNow;
                var txDate = cmd.TransactionDateUtc.Kind == DateTimeKind.Utc
                    ? cmd.TransactionDateUtc
                    : cmd.TransactionDateUtc.ToUniversalTime();

                var transaction = new Transaction
                {
                    Id = Guid.NewGuid(),
                    ProfileId = cmd.ProfileId,
                    InstrumentId = cmd.InstrumentId,
                    Type = cmd.Type,
                    Quantity = cmd.Quantity,
                    Price = cmd.Price,
                    Amount = cmd.Amount > 0 ? cmd.Amount : cmd.Quantity * cmd.Price,
                    TransactionDate = txDate,
                    Notes = cmd.Notes?.Trim() ?? string.Empty,
                    ClientTxnId = cmd.ClientTxnId,
                    Source = source,
                    CreatedAtUtc = now,
                    UpdatedAtUtc = now
                };

                _context.Transactions.Add(transaction);
                await _context.SaveChangesAsync(ct);

                var snapshot = await strategy.ComputeHoldingAsync(cmd.ProfileId, cmd.InstrumentId, now, ct);

                await UpsertHoldingAsync(cmd.ProfileId, cmd.InstrumentId, snapshot, ct);
                await _context.SaveChangesAsync(ct);

                return Result<TransactionResponse>.Success(MapToResponse(transaction, instrument), "Transaction created successfully", 201);
            }
            catch (Exception ex)
            {
                return Result<TransactionResponse>.InternalServerError($"Error processing transaction: {ex.Message}");
            }
        }

        private async Task UpsertHoldingAsync(Guid profileId, Guid instrumentId, HoldingSnapshot snapshot, CancellationToken ct)
        {
            var existing = await _context.Holdings
                .FirstOrDefaultAsync(h => h.ProfileId == profileId && h.InstrumentId == instrumentId, ct);

            if (snapshot.Quantity <= 0)
            {
                if (existing != null)
                    _context.Holdings.Remove(existing);
                return;
            }

            if (existing != null)
            {
                existing.Quantity = snapshot.Quantity;
                existing.AvgPrice = snapshot.AvgPrice;
                existing.CurrentPrice = snapshot.CurrentPrice;
                existing.MarketValue = snapshot.MarketValue;
                existing.UnrealizedPnL = snapshot.UnrealizedPnL;
                existing.RealizedPnL = snapshot.RealizedPnL;
                existing.AccruedInterest = snapshot.AccruedInterest;
                existing.Snapshot = snapshot.Snapshot;
                existing.LastUpdated = DateTime.UtcNow;
            }
            else
            {
                _context.Holdings.Add(new Holding
                {
                    Id = Guid.NewGuid(),
                    ProfileId = profileId,
                    InstrumentId = instrumentId,
                    Quantity = snapshot.Quantity,
                    AvgPrice = snapshot.AvgPrice,
                    CurrentPrice = snapshot.CurrentPrice,
                    MarketValue = snapshot.MarketValue,
                    UnrealizedPnL = snapshot.UnrealizedPnL,
                    RealizedPnL = snapshot.RealizedPnL,
                    AccruedInterest = snapshot.AccruedInterest,
                    Snapshot = snapshot.Snapshot,
                    LastUpdated = DateTime.UtcNow
                });
            }
        }

        private static TransactionResponse MapToResponse(Transaction t, Domain.Entities.Instrument? inst = null)
        {
            var instrument = inst ?? t.Instrument;
            return new TransactionResponse
            {
                Id = t.Id,
                ProfileId = t.ProfileId,
                InstrumentId = t.InstrumentId,
                InstrumentName = instrument?.Name ?? string.Empty,
                InstrumentSymbol = instrument?.Symbol ?? string.Empty,
                Type = t.Type,
                Quantity = t.Quantity,
                Price = t.Price,
                Amount = t.Amount,
                TransactionDate = t.TransactionDate,
                Notes = t.Notes
            };
        }
    }
}
