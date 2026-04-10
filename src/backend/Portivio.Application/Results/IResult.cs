namespace Portivio.Application.Results
{
    public interface IResult
    {
        bool IsSuccess { get; }
        bool IsFailure { get; }
        string Message { get; }
        IReadOnlyList<string> Errors { get; }
        int? StatusCode { get; }
    }

    public interface IResult<T> : IResult
    {
        T? Data { get; }
    }
}
