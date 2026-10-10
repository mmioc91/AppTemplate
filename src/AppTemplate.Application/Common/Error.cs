namespace AppTemplate.Application.Common;

// An expected failure of a use case (ADR 0008). Clients switch on Code; Message is English text for logs.
public sealed record Error
{
    public const string ValidationFailedCode = "VALIDATION_FAILED";

    private static readonly IReadOnlyDictionary<string, string[]> NoFieldErrors = new Dictionary<string, string[]>();

    private Error(string code, ErrorKind kind, string message, IReadOnlyDictionary<string, string[]> fieldErrors)
    {
        Code = code;
        Kind = kind;
        Message = message;
        FieldErrors = fieldErrors;
    }

    public string Code { get; }

    public ErrorKind Kind { get; }

    public string Message { get; }

    // Validation errors only: input field name → error codes for that field.
    public IReadOnlyDictionary<string, string[]> FieldErrors { get; }

    public static Error NotFound(string code, string message) => Create(code, ErrorKind.NotFound, message);

    public static Error Conflict(string code, string message) => Create(code, ErrorKind.Conflict, message);

    public static Error Forbidden(string code, string message) => Create(code, ErrorKind.Forbidden, message);

    public static Error Validation(IReadOnlyDictionary<string, string[]> fieldErrors)
    {
        ArgumentNullException.ThrowIfNull(fieldErrors);

        if (fieldErrors.Count == 0)
        {
            throw new ArgumentException("A validation error needs at least one field error.", nameof(fieldErrors));
        }

        return new Error(ValidationFailedCode, ErrorKind.Validation, "Input validation failed.", fieldErrors);
    }

    // Value equality must include the field errors' contents; the dictionary itself compares by reference.
    public bool Equals(Error? other) =>
        other is not null
        && Code == other.Code
        && Kind == other.Kind
        && Message == other.Message
        && FieldErrors.Count == other.FieldErrors.Count
        && FieldErrors.All(field =>
            other.FieldErrors.TryGetValue(field.Key, out string[]? codes) && codes.SequenceEqual(field.Value));

    public override int GetHashCode() => HashCode.Combine(Code, Kind, Message, FieldErrors.Count);

    private static Error Create(string code, ErrorKind kind, string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        return new Error(code, kind, message, NoFieldErrors);
    }
}
