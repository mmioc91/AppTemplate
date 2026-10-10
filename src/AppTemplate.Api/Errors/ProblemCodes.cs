namespace AppTemplate.Api.Errors;

// Codes for errors the framework produces itself (bad JSON, unknown route, unhandled exception), so every
// ProblemDetails a client sees carries a code (ADR 0018). Errors from Domain and Application bring their own code.
internal static class ProblemCodes
{
    public const string Key = "code";

    public const string MalformedRequest = "MALFORMED_REQUEST";
    public const string Unauthenticated = "UNAUTHENTICATED";
    public const string Forbidden = "FORBIDDEN";
    public const string NotFound = "NOT_FOUND";
    public const string MethodNotAllowed = "METHOD_NOT_ALLOWED";
    public const string UnsupportedMediaType = "UNSUPPORTED_MEDIA_TYPE";
    public const string RequestRejected = "REQUEST_REJECTED";
    public const string UnexpectedError = "UNEXPECTED_ERROR";

    public static string ForStatus(int statusCode) => statusCode switch
    {
        StatusCodes.Status400BadRequest => MalformedRequest,
        StatusCodes.Status401Unauthorized => Unauthenticated,
        StatusCodes.Status403Forbidden => Forbidden,
        StatusCodes.Status404NotFound => NotFound,
        StatusCodes.Status405MethodNotAllowed => MethodNotAllowed,
        StatusCodes.Status415UnsupportedMediaType => UnsupportedMediaType,
        >= StatusCodes.Status500InternalServerError => UnexpectedError,
        _ => RequestRejected,
    };

    // UPPER_SNAKE_CASE (ADR 0004): starts with a letter, then capitals, digits and underscores.
    public static bool IsWellFormed(object? code) =>
        code is string { Length: > 0 } text
        && char.IsAsciiLetterUpper(text[0])
        && text.All(c => char.IsAsciiLetterUpper(c) || char.IsAsciiDigit(c) || c == '_');
}
