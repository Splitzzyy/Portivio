using Portivio.Domain.Enums;

namespace Portivio.Application.Services.Strategies
{
    public class AssetStrategyResolver
    {
        private readonly IReadOnlyDictionary<AssetCategory, IAssetStrategy> _map;

        public AssetStrategyResolver(IEnumerable<IAssetStrategy> strategies)
        {
            _map = strategies.ToDictionary(s => s.Category);
        }

        public IAssetStrategy For(AssetCategory category) =>
            _map.TryGetValue(category, out var strategy)
                ? strategy
                : throw new NotSupportedException($"No strategy registered for asset category '{category}'");
    }
}
