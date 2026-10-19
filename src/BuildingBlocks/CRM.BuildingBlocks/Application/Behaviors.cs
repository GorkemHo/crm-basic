using System.Diagnostics;
using FluentValidation;
using Microsoft.Extensions.Logging;
using ValidationException = CRM.BuildingBlocks.Exceptions.ValidationException;

namespace CRM.BuildingBlocks.Application;

/// <summary>Logs every request with its duration; failures are logged by the exception handler.</summary>
internal sealed class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger;

    public LoggingBehavior(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    {
        _logger = logger;
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var started = Stopwatch.GetTimestamp();

        var response = await next();

        _logger.LogDebug("Handled {Request} in {ElapsedMs:0.0} ms",
            typeof(TRequest).Name, Stopwatch.GetElapsedTime(started).TotalMilliseconds);

        return response;
    }
}

/// <summary>
/// Runs all FluentValidation validators for the request before the handler and turns failures
/// into a <see cref="ValidationException"/> with per-field errors (400).
/// </summary>
internal sealed class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;

    public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators)
    {
        _validators = validators;
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var failures = new List<FluentValidation.Results.ValidationFailure>();

        foreach(var validator in _validators)
        {
            var result = await validator.ValidateAsync(request, cancellationToken);
            failures.AddRange(result.Errors);
        }

        if(failures.Count > 0)
        {
            var errors = failures
                .GroupBy(x => ToCamelCase(x.PropertyName))
                .ToDictionary(g => g.Key, g => g.Select(x => x.ErrorMessage).Distinct().ToArray());

            throw new ValidationException(errors);
        }

        return await next();
    }

    private static string ToCamelCase(string name) =>
        string.IsNullOrEmpty(name) ? name : char.ToLowerInvariant(name[0]) + name[1..];
}
