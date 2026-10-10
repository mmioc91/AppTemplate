namespace AppTemplate.Application.Common;

// How Api maps an expected failure to HTTP (ADR 0008): Validation → 400, NotFound → 404, Conflict → 409, Forbidden → 403.
public enum ErrorKind
{
    Validation,
    NotFound,
    Conflict,
    Forbidden,
}
