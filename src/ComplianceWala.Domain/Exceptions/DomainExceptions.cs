namespace ComplianceWala.Domain.Exceptions;

/// <summary>
/// Thrown when a business rule within the GST domain is violated.
/// Examples: negative ITC amount, invalid GSTIN format, 
/// reconciliation session with zero invoices.
/// 
/// This exception must NEVER contain infrastructure details 
/// (no SQL errors, no HTTP status codes).
/// </summary>
public class DomainException : Exception
{
    public string ErrorCode { get; }

    public DomainException(string errorCode, string message)
        : base(message)
    {
        ErrorCode = errorCode;
    }

    public DomainException(string errorCode, string message, Exception innerException)
        : base(message, innerException)
    {
        ErrorCode = errorCode;
    }
}