using System.Reflection;
using CRM.BuildingBlocks.Messaging;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CRM.BuildingBlocks.Application;

public static class ApplicationServiceCollectionExtensions
{
    /// <summary>Registers <see cref="ISender"/> and the default pipeline (logging → validation → handler). Idempotent.</summary>
    public static IServiceCollection AddDispatcher(this IServiceCollection services)
    {
        services.TryAddScoped<ISender, Sender>();

        if(!services.Any(x => x.ServiceType == typeof(IPipelineBehavior<,>)))
        {
            services.AddScoped(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
            services.AddScoped(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        }

        return services;
    }

    /// <summary>
    /// Registers every request handler, integration event handler and FluentValidation validator in
    /// <paramref name="assembly"/> (public or internal).
    /// </summary>
    public static IServiceCollection AddRequestHandlersFromAssembly(this IServiceCollection services, Assembly assembly)
    {
        services.AddDispatcher();

        var handlerTypes = assembly.GetTypes()
            .Where(t => t is { IsAbstract: false, IsInterface: false, IsGenericTypeDefinition: false });

        foreach(var type in handlerTypes)
        {
            foreach(var handlerInterface in type.GetInterfaces()
                         .Where(i => i.IsGenericType &&
                                     (i.GetGenericTypeDefinition() == typeof(IRequestHandler<,>) ||
                                      i.GetGenericTypeDefinition() == typeof(IIntegrationEventHandler<>))))
            {
                services.AddScoped(handlerInterface, type);
            }
        }

        services.AddValidatorsFromAssembly(assembly, ServiceLifetime.Scoped, includeInternalTypes: true);

        return services;
    }
}
