using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AppTemplate.Api.Auth;
using AppTemplate.Application.Abstractions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace AppTemplate.Integration.Tests.Api.Auth;

// The auth endpoints as a client uses them, for the tests of everything behind them.
internal static class AuthClient
{
    public const string Password = "correct horse battery";

    public static readonly Uri Register = new("/api/v1/auth/register", UriKind.Relative);
    public static readonly Uri Login = new("/api/v1/auth/login", UriKind.Relative);
    public static readonly Uri Refresh = new("/api/v1/auth/refresh", UriKind.Relative);
    public static readonly Uri Logout = new("/api/v1/auth/logout", UriKind.Relative);
    public static readonly Uri Me = new("/api/v1/auth/me", UriKind.Relative);

    // The database is shared by the whole collection, so every registration gets its own address.
    public static string UniqueEmail() => $"user-{Guid.CreateVersion7():N}@example.com";

    public static async Task<TokenPair> RegisterAsync(HttpClient client, string? email = null, string displayName = "Ana")
    {
        HttpResponseMessage response = await client.PostAsJsonAsync(Register, new RegisterRequest(email ?? UniqueEmail(), Password, displayName));
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return await ReadTokensAsync(response);
    }

    // A fresh account without any role, signed in — register gives none. The client is left carrying its access token.
    public static async Task SignInAsync(HttpClient client) =>
        UseBearer(client, (await RegisterAsync(client)).AccessToken);

    // An account with a role register doesn't give: the role is created when the database lacks it (the template
    // seeds none), the user through the identity port, the sign-in through the endpoint. The client carries the token.
    public static async Task SignInWithRoleAsync(ApiFactory factory, HttpClient client, string role)
    {
        string email = UniqueEmail();
        await using (AsyncServiceScope scope = factory.Services.CreateAsyncScope())
        {
            RoleManager<IdentityRole<Guid>> roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
            if (!await roles.RoleExistsAsync(role))
            {
                (await roles.CreateAsync(new IdentityRole<Guid>(role) { Id = Guid.CreateVersion7() })).Succeeded.ShouldBeTrue();
            }

            CreateUserOutcome outcome = await scope.ServiceProvider.GetRequiredService<IIdentityService>()
                .CreateUserAsync(Guid.CreateVersion7(), email, Password, "Rita", role, CancellationToken.None);
            outcome.ShouldBe(CreateUserOutcome.Created);
        }

        HttpResponseMessage response = await LoginAsync(client, email);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        UseBearer(client, (await ReadTokensAsync(response)).AccessToken);
    }

    public static async Task<HttpResponseMessage> LoginAsync(HttpClient client, string email, string password = Password) =>
        await client.PostAsJsonAsync(Login, new LoginRequest(email, password));

    public static async Task<HttpResponseMessage> RefreshAsync(HttpClient client, string refreshToken) =>
        await client.PostAsJsonAsync(Refresh, new RefreshRequest(refreshToken));

    public static async Task<TokenPair> ReadTokensAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<TokenPair>()).ShouldNotBeNull();

    public static async Task<string> ReadCodeAsync(HttpResponseMessage response)
    {
        response.Content.Headers.ContentType.ShouldNotBeNull().MediaType.ShouldBe("application/problem+json");
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return body.RootElement.GetProperty("code").GetString().ShouldNotBeNull();
    }

    public static void UseBearer(HttpClient client, string accessToken) =>
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

    // The token response as the frontend sees it.
    public sealed record TokenPair(string AccessToken, DateTimeOffset AccessTokenExpiresAt, string RefreshToken, DateTimeOffset RefreshTokenExpiresAt);
}
