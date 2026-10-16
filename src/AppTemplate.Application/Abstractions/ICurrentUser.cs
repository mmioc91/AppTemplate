namespace AppTemplate.Application.Abstractions;

// Who is asking (ADR 0031): filled by the Api from the authenticated principal, never from request input.
public interface ICurrentUser
{
    bool IsAuthenticated { get; }

    // The signed-in user's id. Throws when nobody is signed in: with the fallback policy that is a programming error
    // (an endpoint marked anonymous reaching a use case that needs a user), not an expected outcome.
    Guid UserId { get; }

    bool IsInRole(string role);
}
