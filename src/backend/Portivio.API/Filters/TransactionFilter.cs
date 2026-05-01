using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using Portivio.Application.Results;
using Portivio.Infrastructure.Data;

namespace Portivio.API.Filters
{
    /// <summary>
    /// Wraps every write-verb HTTP request (POST/PUT/PATCH/DELETE) in a single
    /// database transaction. Commits on 2xx success Result, rolls back on any
    /// failure Result or unhandled exception. Read verbs are skipped.
    /// </summary>
    public class TransactionFilter : IAsyncActionFilter
    {
        private static readonly HashSet<string> WriteMethods = new(StringComparer.OrdinalIgnoreCase)
        {
            HttpMethods.Post,
            HttpMethods.Put,
            HttpMethods.Patch,
            HttpMethods.Delete
        };

        private readonly PortivioDbContext _context;
        private readonly ILogger<TransactionFilter> _logger;

        public TransactionFilter(PortivioDbContext context, ILogger<TransactionFilter> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var method = context.HttpContext.Request.Method;
            if (!WriteMethods.Contains(method))
            {
                await next();
                return;
            }

            await using var tx = await _context.Database.BeginTransactionAsync();

            var executed = await next();

            if (executed.Exception != null && !executed.ExceptionHandled)
            {
                _logger.LogWarning("Transaction rolled back due to unhandled exception. Path={Path}",
                    context.HttpContext.Request.Path);
                return; // tx auto-rollback on dispose
            }

            if (!IsSuccess(executed.Result))
            {
                _logger.LogInformation("Transaction rolled back due to non-success result. Path={Path} Status={Status}",
                    context.HttpContext.Request.Path,
                    context.HttpContext.Response.StatusCode);
                return; // tx auto-rollback on dispose
            }

            await tx.CommitAsync();
        }

        private static bool IsSuccess(IActionResult? result)
        {
            if (result is ObjectResult obj)
            {
                if (obj.StatusCode is int code && (code < 200 || code >= 300))
                    return false;

                return InspectResultValue(obj.Value);
            }

            if (result is StatusCodeResult sc)
                return sc.StatusCode >= 200 && sc.StatusCode < 300;

            return result is OkResult or CreatedResult or CreatedAtActionResult or CreatedAtRouteResult or NoContentResult;
        }

        private static bool InspectResultValue(object? value)
        {
            if (value is null)
                return true;

            // Application Result / Result<T> pattern carries IsSuccess
            var isSuccessProp = value.GetType().GetProperty(nameof(Result.IsSuccess));
            if (isSuccessProp?.GetValue(value) is bool flag)
                return flag;

            return true;
        }
    }
}
