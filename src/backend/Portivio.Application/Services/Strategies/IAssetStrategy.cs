using Portivio.Application.Results;
using Portivio.Domain.Entities;
using Portivio.Domain.Enums;
using System.Text.Json;

namespace Portivio.Application.Services.Strategies
{
    public sealed record HoldingSnapshot(
        decimal Quantity,
        decimal AvgPrice,
        decimal CurrentPrice,
        decimal MarketValue,
        decimal UnrealizedPnL,
        decimal RealizedPnL,
        decimal AccruedInterest,
        JsonDocument? Snapshot);

    public interface IAssetStrategy
    {
        AssetCategory Category { get; }
        Result ValidateInstrumentMetadata(JsonDocument? meta);
        Result ValidateTransaction(Transaction tx, Instrument inst);
        Task<HoldingSnapshot> ComputeHoldingAsync(Guid profileId, Guid instrumentId, DateTime asOfUtc, CancellationToken ct);
        Task<decimal?> FetchCurrentPriceAsync(Instrument inst, CancellationToken ct);
    }
}
