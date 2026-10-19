using System.Net;
using System.Text.Json;
using CRM.Core.Common;

namespace CRM.API.Middleware
{
    public class GlobalExceptionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<GlobalExceptionMiddleware> _logger;

        public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch(Exception exception)
            {
                await HandleExceptionAsync(context, exception);
            }
        }

        private async Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            var statusCode = exception switch
            {
                UnauthorizedAccessException => (int)HttpStatusCode.Unauthorized,
                InvalidOperationException => (int)HttpStatusCode.BadRequest,
                KeyNotFoundException => (int)HttpStatusCode.NotFound,
                _ => (int)HttpStatusCode.InternalServerError
            };

            // Client errors are expected outcomes; only unhandled failures are logged as errors.
            if(statusCode >= 500)
            {
                _logger.LogError(exception, "Unhandled exception for {Method} {Path}", context.Request.Method, context.Request.Path);
            }
            else
            {
                _logger.LogWarning("{StatusCode} for {Method} {Path}: {Message}", statusCode, context.Request.Method, context.Request.Path, exception.Message);
            }

            context.Response.ContentType = "application/json";
            context.Response.StatusCode = statusCode;

            var response = new ExceptionResponse
            {
                StatusCode = statusCode,
                // Never leak internal exception details to clients.
                Message = statusCode >= 500 ? "An unexpected error occurred." : exception.Message,
                Timestamp = DateTime.UtcNow
            };

            var json = JsonSerializer.Serialize(response, JsonSerializerOptions.Web);

            await context.Response.WriteAsync(json);
        }
    }
}
