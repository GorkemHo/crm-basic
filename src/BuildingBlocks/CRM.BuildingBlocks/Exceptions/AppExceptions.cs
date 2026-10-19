namespace CRM.BuildingBlocks.Exceptions;

/// <summary>400 – the request is malformed or has invalid values.</summary>
public class ValidationException : AppException
{
    public ValidationException(string message, string errorCode = "validation_failed")
        : base(message, errorCode)
    {
    }

    public ValidationException(string field, string message, string errorCode = "validation_failed")
        : base(message, errorCode)
    {
        Errors = new Dictionary<string, string[]> { [field] = [message] };
    }

    public ValidationException(IReadOnlyDictionary<string, string[]> errors)
        : base("One or more validation errors occurred.", "validation_failed")
    {
        Errors = errors;
    }

    public override int StatusCode => 400;

    /// <summary>Optional per-field errors, returned as <c>errors</c> like ASP.NET model validation.</summary>
    public IReadOnlyDictionary<string, string[]>? Errors { get; }
}

/// <summary>401 – the caller could not be authenticated.</summary>
public class UnauthorizedException : AppException
{
    public UnauthorizedException(string message = "Authentication is required.", string errorCode = "unauthorized")
        : base(message, errorCode)
    {
    }

    public override int StatusCode => 401;
}

/// <summary>403 – the caller is authenticated but not allowed to do this.</summary>
public class ForbiddenException : AppException
{
    public ForbiddenException(string message = "You do not have permission to perform this action.", string errorCode = "forbidden")
        : base(message, errorCode)
    {
    }

    public override int StatusCode => 403;
}

/// <summary>404 – a referenced resource does not exist.</summary>
public class NotFoundException : AppException
{
    public NotFoundException(string resource, object key)
        : base($"{resource} '{key}' was not found.", $"{ToCode(resource)}.not_found")
    {
    }

    public NotFoundException(string message, string errorCode)
        : base(message, errorCode)
    {
    }

    public override int StatusCode => 404;

    private static string ToCode(string resource) => resource.Replace(' ', '_').ToLowerInvariant();
}

/// <summary>409 – the request conflicts with the current state (duplicates, resource in use...).</summary>
public class ConflictException : AppException
{
    public ConflictException(string message, string errorCode = "conflict")
        : base(message, errorCode)
    {
    }

    public override int StatusCode => 409;
}

/// <summary>422 – the request is well-formed but violates a business rule.</summary>
public class BusinessRuleException : AppException
{
    public BusinessRuleException(string message, string errorCode = "business_rule_violated")
        : base(message, errorCode)
    {
    }

    public override int StatusCode => 422;
}
