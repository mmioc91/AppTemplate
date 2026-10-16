using AppTemplate.Application.Abstractions;
using AppTemplate.Infrastructure.Identity;
using AppTemplate.Integration.Tests.Api;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Shouldly;

namespace AppTemplate.Integration.Tests.Persistence.Auth;

// What the auth ports need beyond AddInfrastructure, as the Api would provide it: a clock and bound JwtOptions.
internal static class AuthServices
{
    public const string Password = "correct horse battery";

    public static readonly DateTimeOffset Start = new(2030, 6, 15, 12, 0, 0, TimeSpan.Zero);

    public static void Configure(IServiceCollection services)
    {
        services.AddSingleton<TimeProvider>(new FakeTimeProvider(Start));
        services.AddOptions<JwtOptions>().Configure(options =>
        {
            options.Issuer = "AppTemplate";
            options.Audience = "AppTemplate";
            options.SigningKey = ApiFactory.SigningKey;
        });
    }

    // A fresh plain account through the real identity port, so refresh tokens have a user to belong to.
    public static async Task<Guid> CreateUserAsync(ServiceProvider services)
    {
        await using AsyncServiceScope scope = services.CreateAsyncScope();
        var id = Guid.CreateVersion7();
        CreateUserOutcome outcome = await scope.ServiceProvider.GetRequiredService<IIdentityService>()
            .CreateUserAsync(id, $"user-{id:N}@example.com", Password, "Ana", role: null, CancellationToken.None);
        outcome.ShouldBe(CreateUserOutcome.Created);
        return id;
    }
}
