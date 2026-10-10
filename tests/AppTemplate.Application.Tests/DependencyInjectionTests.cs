using AppTemplate.Application.Common;
using AppTemplate.Application.Tests.Samples;
using Microsoft.Extensions.DependencyInjection;
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

    // Factory registrations escape ValidateOnBuild, so resolve each one: a missing dependency fails here, not in production.
    [Theory]
    [MemberData(nameof(RegisteredCommandContracts))]
    public void AddApplication_EveryRegisteredCommandHandlerResolvesWrappedInValidation(Type contract)
    {
        // Arrange
        using ServiceProvider provider = BuildProvider();
        using IServiceScope scope = provider.CreateScope();

        // Act
        object handler = scope.ServiceProvider.GetRequiredService(contract);

        // Assert
        handler.GetType().GetGenericTypeDefinition().ShouldBeOneOf(typeof(ValidationDecorator<>), typeof(ValidationDecorator<,>));
    }

    [Theory]
    [MemberData(nameof(RegisteredQueryContracts))]
    public void AddApplication_EveryRegisteredQueryHandlerResolves(Type contract)
    {
        // Arrange
        using ServiceProvider provider = BuildProvider();
        using IServiceScope scope = provider.CreateScope();

        // Act
        object handler = scope.ServiceProvider.GetRequiredService(contract);

        // Assert
        handler.ShouldBeAssignableTo(contract);
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
    }

    [Theory]
    [InlineData(typeof(CreateSampleHandler))]
    [InlineData(typeof(RenameSampleHandler))]
    public void AddUseCases_DoesNotExposeTheConcreteCommandHandler(Type concreteHandler)
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

    // The use cases in Application plus the samples in this assembly. Register every port a handler may need as a
    // substitute here; Infrastructure and the Api provide the real ones.
    private static ServiceCollection BuildServices(ISampleStore store)
    {
        ServiceCollection services = new();
        services.AddApplication();
        services.AddUseCases(typeof(DependencyInjectionTests).Assembly);
        services.AddSingleton(store);
        return services;
    }

    private ServiceProvider BuildProvider() =>
        BuildServices(_store).BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
}
