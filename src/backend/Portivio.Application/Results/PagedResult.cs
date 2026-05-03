namespace Portivio.Application.Results
{
    public class PagedResult<T>
    {
        public IReadOnlyList<T> Items { get; init; } = Array.Empty<T>();
        public int Page { get; init; }
        public int PageSize { get; init; }
        public int Total { get; init; }
        public bool HasMore => (long)Page * PageSize < Total;

        public static PagedResult<T> Create(IReadOnlyList<T> items, int page, int pageSize, int total) =>
            new() { Items = items, Page = page, PageSize = pageSize, Total = total };
    }
}
