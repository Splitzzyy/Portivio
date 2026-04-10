namespace Portivio.Application.Results
{
    public static class ResultExtensions
    {
        public static TResult Match<TResult>(
            this IResult result,
            Func<TResult> onSuccess,
            Func<IResult, TResult> onFailure)
        {
            return result.IsSuccess ? onSuccess() : onFailure(result);
        }

        public static async Task<TResult> MatchAsync<TResult>(
            this IResult result,
            Func<Task<TResult>> onSuccess,
            Func<IResult, Task<TResult>> onFailure)
        {
            return result.IsSuccess ? await onSuccess() : await onFailure(result);
        }

        public static TResult Match<T, TResult>(
            this IResult<T> result,
            Func<T?, TResult> onSuccess,
            Func<IResult<T>, TResult> onFailure)
        {
            return result.IsSuccess ? onSuccess(result.Data) : onFailure(result);
        }

        public static async Task<TResult> MatchAsync<T, TResult>(
            this IResult<T> result,
            Func<T?, Task<TResult>> onSuccess,
            Func<IResult<T>, Task<TResult>> onFailure)
        {
            return result.IsSuccess ? await onSuccess(result.Data) : await onFailure(result);
        }

        public static void OnSuccess(this IResult result, Action onSuccess)
        {
            if (result.IsSuccess)
                onSuccess();
        }

        public static void OnSuccess<T>(this IResult<T> result, Action<T?> onSuccess)
        {
            if (result.IsSuccess)
                onSuccess(result.Data);
        }

        public static void OnFailure(this IResult result, Action<IResult> onFailure)
        {
            if (result.IsFailure)
                onFailure(result);
        }

        public static void OnFailure<T>(this IResult<T> result, Action<IResult<T>> onFailure)
        {
            if (result.IsFailure)
                onFailure(result);
        }

        public static Result<T> Ensure<T>(this Result<T> result, Func<T?, bool> predicate, string errorMessage) where T : class
        {
            if (result.IsFailure)
                return result;

            if (!predicate(result.Data))
                return Result<T>.BadRequest(errorMessage);

            return result;
        }

        public static async Task<Result<T>> EnsureAsync<T>(this Task<Result<T>> resultTask, Func<T?, Task<bool>> predicate, string errorMessage) where T : class
        {
            var result = await resultTask;

            if (result.IsFailure)
                return result;

            if (!await predicate(result.Data))
                return Result<T>.BadRequest(errorMessage);

            return result;
        }
    }
}
