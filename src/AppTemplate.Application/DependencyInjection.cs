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
        services.AddLogging(); // the decorators' ILogger<T>; idempotent, so the host's providers stay

        return services.AddUseCases(typeof(DependencyInjection).Assembly);
    }

    // Registers every validator and every handler in the assembly. Only the decorated contract is registered — the
    // concrete handler is created inside the factory — so nothing can resolve a handler without its logging and
    // validation. Internal so tests can register their own sample use cases the same way.
    internal static IServiceCollection AddUseCases(this IServiceCollection services, Assembly assembly)
    {
        services.AddValidatorsFromAssembly(assembly, includeInternalTypes: true);

        foreach (Type handler in assembly.GetTypes().Where(t => t is { IsClass: true, IsAbstract: false, IsGenericTypeDefinition: false }))
        {
            // Logging → Validation → handler (ADR 0028): a refused input is logged with its code too.
            foreach (Type contract in handler.GetInterfaces().Where(IsHandler(typeof(ICommandHandler<>))))
            {
                AddDecorated(services, contract, handler, typeof(LoggingDecorator<>), typeof(ValidationDecorator<>));
            }

            foreach (Type contract in handler.GetInterfaces().Where(IsHandler(typeof(ICommandHandler<,>))))
            {
                AddDecorated(services, contract, handler, typeof(LoggingDecorator<,>), typeof(ValidationDecorator<,>));
            }

            // A query without a validator passes through its validation decorator unchanged (ADR 0022).
            foreach (Type contract in handler.GetInterfaces().Where(IsHandler(typeof(IQueryHandler<,>))))
            {
                AddDecorated(services, contract, handler, typeof(QueryLoggingDecorator<,>), typeof(QueryValidationDecorator<,>));
            }
        }

        return services;
    }

    // The handler wrapped in the open decorators, innermost last: the first one given is the outermost.
    private static void AddDecorated(IServiceCollection services, Type contract, Type handler, params Type[] openDecorators) =>
        services.AddScoped(contract, sp =>
        {
            Type[] arguments = contract.GetGenericArguments();
            object current = ActivatorUtilities.CreateInstance(sp, handler);

            foreach (Type openDecorator in openDecorators.Reverse())
            {
                current = ActivatorUtilities.CreateInstance(sp, openDecorator.MakeGenericType(arguments), current);
            }

            return current;
        });

    private static Func<Type, bool> IsHandler(Type openContract) =>
        type => type.IsGenericType && type.GetGenericTypeDefinition() == openContract;
}
