namespace AppTemplate.Application.Abstractions;

// A successful rotation: whose token it was, and the replacement to hand to the client.
public sealed record RefreshTokenRotation(Guid UserId, RefreshToken Replacement);
