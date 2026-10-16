using AppTemplate.Application.Common;

namespace AppTemplate.Application.Auth;

// Expected failures of the auth use cases (ADR 0008, 0031). API contract: never rename or reuse a code.
public static class AuthErrors
{
    public const string InvalidCredentialsCode = "AUTH_INVALID_CREDENTIALS";
    public const string InvalidRefreshTokenCode = "AUTH_INVALID_REFRESH_TOKEN";
    public const string EmailTakenCode = "AUTH_EMAIL_TAKEN";
    public const string UserNotFoundCode = "AUTH_USER_NOT_FOUND";

    // One code for an unknown email, a wrong password and a locked-out account: the response never reveals which.
    public static Error InvalidCredentials() =>
        Error.Unauthorized(InvalidCredentialsCode, "The email or password is wrong, or the account is locked out.");

    // One code for an unknown, expired, revoked or reused refresh token.
    public static Error InvalidRefreshToken() =>
        Error.Unauthorized(InvalidRefreshTokenCode, "The refresh token is unknown, expired, revoked or was already used.");

    public static Error EmailTaken() => Error.Conflict(EmailTakenCode, "An account with this email already exists.");

    // A valid access token whose user no longer exists.
    public static Error UserNotFound() => Error.NotFound(UserNotFoundCode, "The signed-in user no longer exists.");
}
