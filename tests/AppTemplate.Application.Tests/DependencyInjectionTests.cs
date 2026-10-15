using AppTemplate.Application.Abstractions;
using AppTemplate.Application.Auth;
using AppTemplate.Application.Auth.GetCurrentUser;
using AppTemplate.Application.Auth.Register;
using AppTemplate.Application.Common;
using AppTemplate.Application.Tests.Samples;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Shouldly;

namespace AppTemplate.Application.Tests;

public class DependencyInjectionTests
{
    private readonly ISampleStore _store = Substitute.For<ISampleStore>();

    // Every command handler contract registered — the use cases in Application join this list automatically.
    public static TheoryData<Type> RegisteredCommandContracts =>
        new(HandlerContracts(typeof(ICommandHandler<>), typeof(ICommandHandler<,>)));

    // Every query handler contract, likewise.
    public static TheoryData<Type> RegisteredQueryContracts => new(HandlerContracts(typeof(IQueryHandler<,>)));

    // Every concrete handler in Application and in this assembly's samples, found by scanning like AddUseCases does.
    public static TheoryData<Type> ConcreteHandlerTypes => new(ConcreteHandlers());

    // Factory registrations escape ValidateOnBuild, so resolve each one: a missing dependency fails here, not in production.
    [Theory]
    [MemberData(nameof(RegisteredCommandContracts))]
    public void AddApplication_EveryRegisteredCommandHandlerResolvesWrappedInLoggingThenValidation(Type contract)
    {
        // Arrange
        using ServiceProvider provider = BuildProvider();
        using IServiceScope scope = provider.CreateScope();

        // Act
        object handler = scope.ServiceProvider.GetRequiredService(contract);

        // Assert: the outermost link is logging (ADR 0028); validation underneath is proved by the invalid-command tests.
        handler.GetType().GetGenericTypeDefinition().ShouldBeOneOf(typeof(LoggingDecorator<>), typeof(LoggingDecorator<,>));
    }

    [Theory]
    [MemberData(nameof(RegisteredQueryContracts))]
    public void AddApplication_EveryRegisteredQueryHandlerResolvesWrappedInLoggingThenValidation(Type contract)
    {
        // Arrange
        using ServiceProvider provider = BuildProvider();
        using IServiceScope scope = provider.CreateScope();

        // Act
        object handler = scope.ServiceProvider.GetRequiredService(contract);

        // Assert
        handler.GetType().GetGenericTypeDefinition().ShouldBe(typeof(QueryLoggingDecorator<,>));
    }

    [Fact]
    public async Task AddUseCases_InvalidCommand_IsLoggedWithTheValidationCode()
    {
        // Arrange: logging sits outside validation, so a refused input is logged too (ADR 0028).
        ServiceCollection services = BuildServices(_store);
        services.AddLogging(logging => logging.AddFakeLogging());
        using ServiceProvider provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        using IServiceScope scope = provider.CreateScope();
        ICommandHandler<CreateSample, Guid> handler = scope.ServiceProvider.GetRequiredService<ICommandHandler<CreateSample, Guid>>();

        // Act
        await handler.HandleAsync(new CreateSample(""), CancellationToken.None);

        // Assert
        FakeLogRecord record = provider.GetFakeLogCollector().LatestRecord;
        record.Level.ShouldBe(LogLevel.Information);
        record.GetStructuredStateValue("UseCase").ShouldBe(nameof(CreateSample));
        record.GetStructuredStateValue("ErrorCode").ShouldBe(Error.ValidationFailedCode);
    }

    [Fact]
    public void AddUseCases_RegistersEveryKindOfHandler()
    {
        // Act
        Type[] commands = HandlerContracts(typeof(ICommandHandler<>), typeof(ICommandHandler<,>));
        Type[] queries = HandlerContracts(typeof(IQueryHandler<,>));

        // Assert: guards the scan itself — an empty list would make the theories above pass vacuously.
        commands.ShouldContain(typeof(ICommandHandler<CreateSample, Guid>));
        commands.ShouldContain(typeof(ICommandHandler<RenameSample>));
        queries.ShouldContain(typeof(IQueryHandler<GetSample, string>));

        // Application's own use cases are found by the same scan.
        commands.ShouldContain(typeof(ICommandHandler<RegisterCommand, TokenPairResponse>));
        queries.ShouldContain(typeof(IQueryHandler<GetCurrentUserQuery, CurrentUserResponse>));
    }

    [Fact]
    public void ConcreteHandlerScan_FindsEveryKindOfSampleHandlerButNoDecorator()
    {
        // Act
        Type[] handlers = ConcreteHandlers();

        // Assert: guards the scan, like the test above.
        handlers.ShouldContain(typeof(CreateSampleHandler));
        handlers.ShouldContain(typeof(RenameSampleHandler));
        handlers.ShouldContain(typeof(GetSampleHandler));
        handlers.ShouldNotContain(t => t.IsGenericTypeDefinition);
    }

    [Theory]
    [MemberData(nameof(ConcreteHandlerTypes))]
    public void AddUseCases_DoesNotExposeTheConcreteHandler(Type concreteHandler)
    {
        // Arrange
        using ServiceProvider provider = BuildProvider();
        using IServiceScope scope = provider.CreateScope();

        // Act
        object? handler = scope.ServiceProvider.GetService(concreteHandler);

        // Assert: resolving the handler directly would skip validation.
        handler.ShouldBeNull();
    }

    [Fact]
    public async Task AddUseCases_InvalidCommand_IsStoppedBeforeTheHandlerTouchesAnyPort()
    {
        // Arrange
        using ServiceProvider provider = BuildProvider();
        using IServiceScope scope = provider.CreateScope();
        ICommandHandler<CreateSample, Guid> handler = scope.ServiceProvider.GetRequiredService<ICommandHandler<CreateSample, Guid>>();

        // Act
        Result<Guid> result = await handler.HandleAsync(new CreateSample(""), CancellationToken.None);

        // Assert
        Error error = result.Error.ShouldNotBeNull();
        error.FieldErrors[nameof(CreateSample.Name)].ShouldBe([ValidationCodes.Required]);
        _store.DidNotReceiveWithAnyArgs().Add(default!);
    }

    [Fact]
    public void AddApplication_WithoutTimeProvider_RegistersTheSystemClock()
    {
        // Arrange
        using ServiceProvider provider = BuildProvider();

        // Act
        TimeProvider time = provider.GetRequiredService<TimeProvider>();

        // Assert
        time.ShouldBeSameAs(TimeProvider.System);
    }

    [Fact]
    public void AddApplication_AfterAnotherTimeProvider_KeepsIt()
    {
        // Arrange: tests and hosts may register their own clock first.
        FakeTimeProvider fake = new();
        ServiceCollection services = new();
        services.AddSingleton<TimeProvider>(fake);

        // Act
        services.AddApplication();

        // Assert
        using ServiceProvider provider = services.BuildServiceProvider();
        provider.GetRequiredService<TimeProvider>().ShouldBeSameAs(fake);
    }

    private static Type[] HandlerContracts(params Type[] openContracts) =>
        BuildServices(Substitute.For<ISampleStore>())
            .Select(d => d.ServiceType)
            .Where(t => t.IsGenericType && openContracts.Contains(t.GetGenericTypeDefinition()))
            .ToArray();

    // The decorators are open generics; everything else implementing a handler contract is a use case's handler.
    private static Type[] ConcreteHandlers() =>
        new[] { typeof(ICommandHandler<>).Assembly, typeof(DependencyInjectionTests).Assembly }
            .SelectMany(assembly => assembly.GetTypes())
            .Where(t => t is { IsClass: true, IsAbstract: false, IsGenericTypeDefinition: false })
            .Where(t => t.GetInterfaces().Any(i => i.IsGenericType
                && (i.GetGenericTypeDefinition() == typeof(ICommandHandler<>)
                    || i.GetGenericTypeDefinition() == typeof(ICommandHandler<,>)
                    || i.GetGenericTypeDefinition() == typeof(IQueryHandler<,>))))
            .ToArray();

    // The use cases in Application plus the samples in this assembly. Register every port a handler may need as a
    // substitute here; Infrastructure and the Api provide the real ones.
    private static ServiceCollection BuildServices(ISampleStore store)
    {
        ServiceCollection services = new();
        services.AddApplication();
        services.AddUseCases(typeof(DependencyInjectionTests).Assembly);
        services.AddSingleton(store);

        // The ports the auth use cases need (ADR 0031).
        services.AddSingleton(Substitute.For<IIdentityService>());
        services.AddSingleton(Substitute.For<ITokenService>());
        services.AddSingleton(Substitute.For<ICurrentUser>());
        return services;
    }

    private ServiceProvider BuildProvider() =>
        BuildServices(_store).BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
}
