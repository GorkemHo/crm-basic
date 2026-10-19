using Microsoft.AspNetCore.Http.Features;

namespace CRM.API.ErrorHandling;

public static class ProblemDetailsDefaults
{
    public const string CodeKey = "code";

    /// <summary>
    /// Applied to every problem response (exceptions, model validation, bare 401/403/404 status codes)
    /// so clients always get <c>code</c>, <c>traceId</c> and <c>instance</c>.
    /// </summary>
    public static void Customize(ProblemDetailsContext context)
    {
        var problem = context.ProblemDetails;
        var httpContext = context.HttpContext;

        problem.Instance ??= $"{httpContext.Request.Method} {httpContext.Request.Path}";

        problem.Extensions.TryAdd("traceId",
            httpContext.Features.Get<IHttpActivityFeature>()?.Activity.Id ?? httpContext.TraceIdentifier);

        problem.Extensions.TryAdd(CodeKey, problem.Status switch
        {
            StatusCodes.Status400BadRequest => "validation_failed",
            StatusCodes.Status401Unauthorized => "unauthorized",
            StatusCodes.Status403Forbidden => "forbidden",
            StatusCodes.Status404NotFound => "not_found",
            StatusCodes.Status405MethodNotAllowed => "method_not_allowed",
            _ => "error"
        });
    }
}
