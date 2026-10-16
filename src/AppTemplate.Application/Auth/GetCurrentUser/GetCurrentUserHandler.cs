using AppTemplate.Application.Abstractions;
using AppTemplate.Application.Common;

namespace AppTemplate.Application.Auth.GetCurrentUser;

internal sealed class GetCurrentUserHandler(IIdentityService identity, ICurrentUser currentUser)
    : IQueryHandler<GetCurrentUserQuery, CurrentUserResponse>
{
    public async Task<Result<CurrentUserResponse>> HandleAsync(GetCurrentUserQuery query, CancellationToken cancellationToken)
    {
        UserAccount? user = await identity.FindByIdAsync(currentUser.UserId, cancellationToken);

        if (user is null)
        {
            return AuthErrors.UserNotFound();
        }

        IReadOnlyList<string> roles = await identity.GetRolesAsync(user.Id, cancellationToken);

        return new CurrentUserResponse(user.Id, user.Email, user.DisplayName, roles);
    }
}
