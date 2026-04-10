namespace Portivio.Application.Results
{
    public class Result : IResult
    {
        public bool IsSuccess { get; }
        public bool IsFailure => !IsSuccess;
        public string Message { get; }
        public IReadOnlyList<string> Errors { get; }
        public int? StatusCode { get; }

        protected Result(bool isSuccess, string message, IEnumerable<string>? errors = null, int? statusCode = null)
        {
            IsSuccess = isSuccess;
            Message = message;
            Errors = errors?.ToList().AsReadOnly() ?? new List<string>().AsReadOnly();
            StatusCode = statusCode;
        }

        public static Result Success(string message = "Operation completed successfully", int statusCode = 200)
            => new(true, message, null, statusCode);

        public static Result Failure(string message, int statusCode = 400)
            => new(false, message, new[] { message }, statusCode);

        public static Result Failure(string message, IEnumerable<string> errors, int statusCode = 400)
            => new(false, message, errors, statusCode);

        public static Result BadRequest(string message)
            => new(false, message, new[] { message }, 400);

        public static Result Unauthorized(string message = "Unauthorized access")
            => new(false, message, new[] { message }, 401);

        public static Result Forbidden(string message = "Access forbidden")
            => new(false, message, new[] { message }, 403);

        public static Result NotFound(string message = "Resource not found")
            => new(false, message, new[] { message }, 404);

        public static Result Conflict(string message)
            => new(false, message, new[] { message }, 409);

        public static Result InternalServerError(string message = "An internal server error occurred")
            => new(false, message, new[] { message }, 500);
    }

    public class Result<T> : IResult<T>
    {
        public bool IsSuccess { get; }
        public bool IsFailure => !IsSuccess;
        public string Message { get; }
        public T? Data { get; }
        public IReadOnlyList<string> Errors { get; }
        public int? StatusCode { get; }

        protected Result(bool isSuccess, T? data, string message, IEnumerable<string>? errors = null, int? statusCode = null)
        {
            IsSuccess = isSuccess;
            Data = data;
            Message = message;
            Errors = errors?.ToList().AsReadOnly() ?? new List<string>().AsReadOnly();
            StatusCode = statusCode;
        }

        public static Result<T> Success(T data, string message = "Operation completed successfully", int statusCode = 200)
            => new(true, data, message, null, statusCode);

        public static Result<T> Failure(string message, int statusCode = 400)
            => new(false, default, message, new[] { message }, statusCode);

        public static Result<T> Failure(string message, IEnumerable<string> errors, int statusCode = 400)
            => new(false, default, message, errors, statusCode);

        public static Result<T> BadRequest(string message)
            => new(false, default, message, new[] { message }, 400);

        public static Result<T> Unauthorized(string message = "Unauthorized access")
            => new(false, default, message, new[] { message }, 401);

        public static Result<T> Forbidden(string message = "Access forbidden")
            => new(false, default, message, new[] { message }, 403);

        public static Result<T> NotFound(string message = "Resource not found")
            => new(false, default, message, new[] { message }, 404);

        public static Result<T> Conflict(string message)
            => new(false, default, message, new[] { message }, 409);

        public static Result<T> InternalServerError(string message = "An internal server error occurred")
            => new(false, default, message, new[] { message }, 500);

        public Result<TNew> Map<TNew>(Func<T?, TNew> mapper) where TNew : class
        {
            if (IsFailure)
                return Result<TNew>.Failure(Message, Errors, StatusCode ?? 400);

            try
            {
                var mappedData = mapper(Data);
                return Result<TNew>.Success(mappedData, Message, StatusCode ?? 200);
            }
            catch (Exception ex)
            {
                return Result<TNew>.InternalServerError($"Mapping failed: {ex.Message}");
            }
        }

        public async Task<Result<TNew>> MapAsync<TNew>(Func<T?, Task<TNew>> mapper) where TNew : class
        {
            if (IsFailure)
                return Result<TNew>.Failure(Message, Errors, StatusCode ?? 400);

            try
            {
                var mappedData = await mapper(Data);
                return Result<TNew>.Success(mappedData, Message, StatusCode ?? 200);
            }
            catch (Exception ex)
            {
                return Result<TNew>.InternalServerError($"Mapping failed: {ex.Message}");
            }
        }

        public Result<T> Bind(Func<T?, Result<T>> binder)
        {
            if (IsFailure)
                return this;

            try
            {
                return binder(Data);
            }
            catch (Exception ex)
            {
                return Result<T>.InternalServerError($"Operation failed: {ex.Message}");
            }
        }

        public async Task<Result<T>> BindAsync(Func<T?, Task<Result<T>>> binder)
        {
            if (IsFailure)
                return this;

            try
            {
                return await binder(Data);
            }
            catch (Exception ex)
            {
                return Result<T>.InternalServerError($"Operation failed: {ex.Message}");
            }
        }
    }
}
