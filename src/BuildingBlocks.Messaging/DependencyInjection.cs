using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BuildingBlocks.Messaging;

public static class DependencyInjection
{
    /// <summary>Registers the shared RabbitMQ connection and confirmed publisher.</summary>
    public static IServiceCollection AddBatchyardMessaging(this IServiceCollection services, MessagingOptions options)
    {
        services.TryAddSingleton(options);
        services.TryAddSingleton<RabbitMqConnection>();
        services.TryAddSingleton<IMessagePublisher, RabbitMqPublisher>();
        return services;
    }

    /// <summary>Drains the service's dbo.Outbox table into RabbitMQ in the background.</summary>
    public static IServiceCollection AddOutboxDispatcher(this IServiceCollection services, string connectionString)
    {
        services.AddSingleton(new SqlOutboxStore(connectionString));
        services.AddHostedService<OutboxDispatcher>();
        return services;
    }

    /// <summary>Consumes <paramref name="queue"/> with a scoped <typeparamref name="THandler"/>.</summary>
    public static IServiceCollection AddMessageConsumer<THandler>(this IServiceCollection services, QueueBinding queue)
        where THandler : class, IMessageHandler
    {
        services.AddScoped<THandler>();
        services.AddHostedService(sp => ActivatorUtilities.CreateInstance<RabbitMqConsumer<THandler>>(sp, queue));
        return services;
    }
}
