using CRM.Core.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace CRM.API.ErrorHandling;

/// <summary>
/// Turns exceptions into RFC 7807 problem responses.
/// <see cref="AppException"/>s are expected errors: their status, message and code are returned.
/// Anything else is a 500 whose details are logged but never sent to the client.
/// </summary>
public sealed class AppExceptionHandler : IExceptionHandler
{
    private readonly IProblemDetailsService _problemDetailsService;
    private readonly ILogger<AppExceptionHandler> _logger;

    public AppExceptionHandler(IProblemDetailsService problemDetailsService, ILogger<AppExceptionHandler> logger)
    {
        _problemDetailsService = problemDetailsService;
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        ProblemDetails problem;

        if(exception is AppException appException)
        {
            _logger.LogWarning("{StatusCode} {ErrorCode} for {Method} {Path}: {Message}",
                appException.StatusCode, appException.ErrorCode,
                httpContext.Request.Method, httpContext.Request.Path, appException.Message);

            problem = appException is ValidationException { Errors: not null } validation
                ? new ValidationProblemDetails(validation.Errors.ToDictionary(x => x.Key, x => x.Value))
                : new ProblemDetails();

            problem.Status = appException.StatusCode;
            problem.Detail = appException.Message;
            problem.Extensions[ProblemDetailsDefaults.CodeKey] = appException.ErrorCode;
        }
        else
        {
            _logger.LogError(exception, "Unhandled exception for {Method} {Path}",
                httpContext.Request.Method, httpContext.Request.Path);

            problem = new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Detail = "An unexpected error occurred."
            };
            problem.Extensions[ProblemDetailsDefaults.CodeKey] = "internal_error";
        }

        httpContext.Response.StatusCode = problem.Status.Value;

        return await _problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
            Exception = exception
        });
    }
}
