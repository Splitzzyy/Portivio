using System.Net;

namespace Portivio.API.Middleware;

public class GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger, IWebHostEnvironment env)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception on {Method} {Path}",
                context.Request.Method, context.Request.Path);

            var response = context.Response;
            response.ContentType = "application/json";

            var statusCode = ex switch
            {
                UnauthorizedAccessException => (int)HttpStatusCode.Unauthorized,
                ArgumentException => (int)HttpStatusCode.BadRequest,
                KeyNotFoundException => (int)HttpStatusCode.NotFound,
                _ => (int)HttpStatusCode.InternalServerError
            };

            response.StatusCode = statusCode;

            var result = new
            {
                success = false,
                message = env.IsDevelopment() ? ex.Message : "An unexpected error occurred",
                detail = env.IsDevelopment() ? ex.StackTrace : null
            };

            await response.WriteAsJsonAsync(result);
        }
    }
}
