using CRM.BuildingBlocks.Messaging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CRM.BuildingBlocks.Messaging.RabbitMq;

public static class MessagingServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="IMessagePublisher"/>: RabbitMQ when <c>RabbitMq:Enabled</c> is true, otherwise a no-op.
    /// </summary>
    public static IServiceCollection AddMessaging(this IServiceCollection services, IConfiguration configuration)
    {
        var section = configuration.GetSection(RabbitMqOptions.SectionName);
        services.Configure<RabbitMqOptions>(section);

        if(!section.GetValue<bool>(nameof(RabbitMqOptions.Enabled)))
        {
            services.AddSingleton<IMessagePublisher, NullMessagePublisher>();
            return services;
        }

        services.AddSingleton<RabbitMqConnectionProvider>();
        services.AddSingleton<IMessagePublisher, RabbitMqMessagePublisher>();
        services.AddHealthChecks().AddCheck<RabbitMqHealthCheck>("rabbitmq", tags: ["ready"]);

        return services;
    }

    /// <summary>
    /// Consumes <typeparamref name="TMessage"/> from <paramref name="queueName"/> with <typeparamref name="THandler"/>.
    /// Does nothing while messaging is disabled. Call after <see cref="AddMessaging"/>.
    /// </summary>
    /// <example>
    /// services.AddMessageConsumer&lt;CustomerCreated, SendWelcomeMailHandler&gt;(configuration, "crm.customer-created.welcome-mail");
    /// </example>
    public static IServiceCollection AddMessageConsumer<TMessage, THandler>(
        this IServiceCollection services,
        IConfiguration configuration,
        string queueName,
        string? routingKey = null)
        where TMessage : class
        where THandler : class, IMessageHandler<TMessage>
    {
        if(!configuration.GetSection(RabbitMqOptions.SectionName).GetValue<bool>(nameof(RabbitMqOptions.Enabled)))
        {
            return services;
        }

        services.AddScoped<IMessageHandler<TMessage>, THandler>();
        services.AddSingleton(new RabbitMqConsumerOptions<TMessage>(queueName, routingKey ?? MessageRouting.For<TMessage>()));
        services.AddHostedService<RabbitMqConsumer<TMessage>>();

        return services;
    }
}
