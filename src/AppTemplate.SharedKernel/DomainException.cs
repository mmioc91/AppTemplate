namespace AppTemplate.SharedKernel;

/// <summary>
/// Thrown when a domain invariant is breached. Never used for control flow.
/// </summary>
public class DomainException : Exception
{
    public DomainException(string code, string message) : base(message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        Code = code;
    }

    public DomainException(string code, string message, Exception innerException) : base(message, innerException)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        Code = code;
    }

    // Stable, machine-readable UPPER_SNAKE_CASE code returned to clients; Message is English text for logs only.
    public string Code { get; }
}
