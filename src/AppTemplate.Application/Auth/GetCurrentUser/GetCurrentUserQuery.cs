namespace AppTemplate.Application.Auth.GetCurrentUser;

// The signed-in user as stored now, not as the access token claims were minted. Takes no input: the user comes from
// ICurrentUser.
public sealed record GetCurrentUserQuery;
