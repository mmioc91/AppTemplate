using System.Reflection;
using AppTemplate.Application.Common;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AppTemplate.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);

        return services.AddUseCases(typeof(DependencyInjection).Assembly);
    }

    // Registers every validator and every handler in the assembly. Only the decorated contract is registered — the
    // concrete handler is created inside the factory — so nothing can resolve a handler without its validation.
    // Internal so tests can register their own sample use cases the same way.
    internal static IServiceCollection AddUseCases(this IServiceCollection services, Assembly assembly)
    {
        services.AddValidatorsFromAssembly(assembly, includeInternalTypes: true);

        foreach (Type handler in assembly.GetTypes().Where(t => t is { IsClass: true, IsAbstract: false, IsGenericTypeDefinition: false }))
        {
            foreach (Type contract in handler.GetInterfaces().Where(IsHandler(typeof(ICommandHandler<>))))
            {
                AddDecorated(services, contract, typeof(ValidationDecorator<>), handler);
            }

            foreach (Type contract in handler.GetInterfaces().Where(IsHandler(typeof(ICommandHandler<,>))))
            {
                AddDecorated(services, contract, typeof(ValidationDecorator<,>), handler);
            }

            // A query without a validator passes through its decorator unchanged (ADR 0022).
            foreach (Type contract in handler.GetInterfaces().Where(IsHandler(typeof(IQueryHandler<,>))))
            {
                AddDecorated(services, contract, typeof(QueryValidationDecorator<,>), handler);
            }
        }

        return services;
    }

    private static void AddDecorated(IServiceCollection services, Type contract, Type openDecorator, Type handler)
    {
        Type decorator = openDecorator.MakeGenericType(contract.GetGenericArguments());

        services.AddScoped(contract, sp =>
            ActivatorUtilities.CreateInstance(sp, decorator, ActivatorUtilities.CreateInstance(sp, handler)));
    }

    private static Func<Type, bool> IsHandler(Type openContract) =>
        type => type.IsGenericType && type.GetGenericTypeDefinition() == openContract;
}
