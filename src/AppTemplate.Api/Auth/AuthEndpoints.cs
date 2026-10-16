using AppTemplate.Api.Errors;
using AppTemplate.Application.Auth;
using AppTemplate.Application.Auth.GetCurrentUser;
using AppTemplate.Application.Auth.Login;
using AppTemplate.Application.Auth.Logout;
using AppTemplate.Application.Auth.Refresh;
using AppTemplate.Application.Auth.Register;
using AppTemplate.Application.Common;
using Microsoft.AspNetCore.Http.HttpResults;

namespace AppTemplate.Api.Auth;

// /api/v1/auth (ADR 0031). Register, login and refresh are the anonymous, rate-limited way in; logout and me need the
// access token like everything else.
internal static class AuthEndpoints
{
    public const string RegisterRouteName = "Register";
    public const string LoginRouteName = "Login";
    public const string RefreshRouteName = "Refresh";
    public const string LogoutRouteName = "Logout";
    public const string MeRouteName = "GetCurrentUser";

    public static RouteGroupBuilder MapAuth(this RouteGroupBuilder v1)
    {
        RouteGroupBuilder auth = v1.MapGroup("/auth").WithTags("Auth");

        auth.MapPost("/register", RegisterAsync)
            .WithName(RegisterRouteName)
            .AllowAnonymous()
            .RequireRateLimiting(AuthRateLimiting.PolicyName)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        auth.MapPost("/login", LoginAsync)
            .WithName(LoginRouteName)
            .AllowAnonymous()
            .RequireRateLimiting(AuthRateLimiting.PolicyName)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        auth.MapPost("/refresh", RefreshAsync)
            .WithName(RefreshRouteName)
            .AllowAnonymous()
            .RequireRateLimiting(AuthRateLimiting.RefreshPolicyName)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        auth.MapPost("/logout", LogoutAsync)
            .WithName(LogoutRouteName)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        auth.MapGet("/me", GetCurrentUserAsync)
            .WithName(MeRouteName)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return v1;
    }

    // 201: an account was created. No Location — there is no resource for one user, /me is the signed-in one.
    private static async Task<Results<Created<TokenPairResponse>, ProblemHttpResult>> RegisterAsync(
        RegisterRequest request,
        ICommandHandler<RegisterCommand, TokenPairResponse> handler,
        CancellationToken cancellationToken)
    {
        Result<TokenPairResponse> result = await handler.HandleAsync(
            new RegisterCommand(request.Email, request.Password, request.DisplayName),
            cancellationToken);

        return result.IsSuccess ? TypedResults.Created((string?)null, result.Value) : result.Error.ToProblem();
    }

    private static async Task<Results<Ok<TokenPairResponse>, ProblemHttpResult>> LoginAsync(
        LoginRequest request,
        ICommandHandler<LoginCommand, TokenPairResponse> handler,
        CancellationToken cancellationToken)
    {
        Result<TokenPairResponse> result = await handler.HandleAsync(new LoginCommand(request.Email, request.Password), cancellationToken);

        return result.IsSuccess ? TypedResults.Ok(result.Value) : result.Error.ToProblem();
    }

    private static async Task<Results<Ok<TokenPairResponse>, ProblemHttpResult>> RefreshAsync(
        RefreshRequest request,
        ICommandHandler<RefreshCommand, TokenPairResponse> handler,
        CancellationToken cancellationToken)
    {
        Result<TokenPairResponse> result = await handler.HandleAsync(new RefreshCommand(request.RefreshToken), cancellationToken);

        return result.IsSuccess ? TypedResults.Ok(result.Value) : result.Error.ToProblem();
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> LogoutAsync(
        LogoutRequest request,
        ICommandHandler<LogoutCommand> handler,
        CancellationToken cancellationToken)
    {
        Result result = await handler.HandleAsync(new LogoutCommand(request.RefreshToken), cancellationToken);

        return result.IsSuccess ? TypedResults.NoContent() : result.Error.ToProblem();
    }

    private static async Task<Results<Ok<CurrentUserResponse>, ProblemHttpResult>> GetCurrentUserAsync(
        IQueryHandler<GetCurrentUserQuery, CurrentUserResponse> handler,
        CancellationToken cancellationToken)
    {
        Result<CurrentUserResponse> result = await handler.HandleAsync(new GetCurrentUserQuery(), cancellationToken);

        return result.IsSuccess ? TypedResults.Ok(result.Value) : result.Error.ToProblem();
    }
}
