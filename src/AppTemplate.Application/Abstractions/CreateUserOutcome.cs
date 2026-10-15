namespace AppTemplate.Application.Abstractions;

// Why IIdentityService.CreateUserAsync did or did not create the user. Any other Identity failure is a bug and throws.
public enum CreateUserOutcome
{
    Created,
    EmailTaken,
    EmailInvalid,
    PasswordTooShort,
}
