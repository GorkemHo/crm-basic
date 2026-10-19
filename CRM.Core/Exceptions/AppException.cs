namespace CRM.Core.Exceptions;

/// <summary>
/// Base type for expected, client-facing errors. The API turns these into RFC 7807 problem
/// responses with <see cref="StatusCode"/>, the message as <c>detail</c> and <see cref="ErrorCode"/> as <c>code</c>.
/// Any other exception is treated as an unexpected server error (500) and its message is not exposed.
/// </summary>
public abstract class AppException : Exception
{
    protected AppException(string message, string errorCode)
        : base(message)
    {
        ErrorCode = errorCode;
    }

    /// <summary>HTTP status code the API responds with.</summary>
    public abstract int StatusCode { get; }

    /// <summary>Stable, machine-readable code (e.g. <c>company.tax_number_taken</c>) clients can switch on.</summary>
    public string ErrorCode { get; }
}
