namespace AppTemplate.Application.Common;

// Generic codes for input validation (ADR 0012). The field name says *what* is wrong, the code says *how*, so the
// frontend needs one text per code, not per field. API contract: never rename or reuse a value.
public static class ValidationCodes
{
    public const string Required = "REQUIRED";
    public const string TooShort = "TOO_SHORT";
    public const string TooLong = "TOO_LONG";
    public const string Invalid = "INVALID";
    public const string NotPositive = "NOT_POSITIVE";
    public const string TooLarge = "TOO_LARGE"; // a number over its maximum; TooLong is for text
    public const string NotAllowed = "NOT_ALLOWED";
}
