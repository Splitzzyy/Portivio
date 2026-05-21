namespace Portivio.Application.Services
{
    public record StockQuote(string Symbol, decimal Price, DateTime AsOf, string Source);

    public record MutualFundNav(string Isin, string SchemeName, decimal Nav, DateTime AsOf, string Source);

    public record FdRateEntry(string Bank, int TenureMonths, decimal RatePercent, DateTime AsOf, string Source);

    public record PpfRateEntry(decimal RatePercent, DateTime AsOf, string Source);

    public interface IMutualFundNavProvider
    {
        Task<IReadOnlyList<MutualFundNav>> GetAllNavsAsync(CancellationToken ct = default);
        Task<MutualFundNav?> GetByIsinAsync(string isin, CancellationToken ct = default);
    }

    public interface IStandardRateProvider
    {
        Task<PpfRateEntry> GetPpfRateAsync(CancellationToken ct = default);
        Task<IReadOnlyList<FdRateEntry>> GetFdRatesAsync(CancellationToken ct = default);
    }
}
