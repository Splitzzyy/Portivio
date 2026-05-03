using Portivio.Domain.Enums;
using System.Text.Json;

namespace Portivio.Application.DTOs.Instrument
{
    public class CreateAssetTypeRequest
    {
        public string Name { get; set; } = string.Empty;
    }

    public class AssetTypeResponse
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    public class CreateInstrumentRequest
    {
        public Guid AssetTypeId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Symbol { get; set; } = string.Empty;
        public string Currency { get; set; } = string.Empty;
        public AssetCategory Category { get; set; }
        public string? Isin { get; set; }
        public PriceSource PriceSource { get; set; }
        public string? PriceSourceKey { get; set; }
        public JsonDocument? Metadata { get; set; }
    }

    public class UpdateInstrumentRequest
    {
        public string Name { get; set; } = string.Empty;
        public string Symbol { get; set; } = string.Empty;
        public string Currency { get; set; } = string.Empty;
        public AssetCategory? Category { get; set; }
        public string? Isin { get; set; }
        public PriceSource? PriceSource { get; set; }
        public string? PriceSourceKey { get; set; }
        public JsonDocument? Metadata { get; set; }
    }

    public class InstrumentResponse
    {
        public Guid Id { get; set; }
        public Guid AssetTypeId { get; set; }
        public string AssetTypeName { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Symbol { get; set; } = string.Empty;
        public string Currency { get; set; } = string.Empty;
        public AssetCategory Category { get; set; }
        public string? Isin { get; set; }
        public PriceSource PriceSource { get; set; }
        public string? PriceSourceKey { get; set; }
    }
}
