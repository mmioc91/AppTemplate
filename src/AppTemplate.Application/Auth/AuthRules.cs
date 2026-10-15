namespace AppTemplate.Application.Auth;

// Input limits shared by the validators and the Identity configuration, so each is defined once. The validators apply
// them before a handler runs, so nothing over these limits is ever normalized, looked up or hashed (ADR 0031).
public static class AuthRules
{
    public const int EmailMaxLength = 256;

    // Length beats character classes (NIST): the only password rule, here and in Identity.
    public const int PasswordMinLength = 10;
    public const int PasswordMaxLength = 128;
    public const int DisplayNameMaxLength = 80;

    // A refresh token is 32 random bytes in base64url (43 characters); anything longer is not ours.
    public const int RefreshTokenMaxLength = 64;
}
