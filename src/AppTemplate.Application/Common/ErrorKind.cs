namespace AppTemplate.Application.Common;

// How Api maps an expected failure to HTTP (ADR 0008): Validation → 400, Unauthorized → 401, Forbidden → 403,
// NotFound → 404, Conflict → 409.
public enum ErrorKind
{
    Validation,
    Unauthorized,
    NotFound,
    Conflict,
    Forbidden,
}
