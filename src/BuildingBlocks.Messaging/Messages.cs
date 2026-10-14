namespace BuildingBlocks.Messaging;

/// <summary>An integration event. <see cref="Type"/> doubles as the RabbitMQ routing key.</summary>
public sealed record IntegrationMessage(Guid Id, string Type, string Payload);

/// <summary>Settings shared by the publisher and consumers.</summary>
public sealed class MessagingOptions
{
    /// <summary>AMQP URI, for example amqp://guest:guest@rabbitmq:5672.</summary>
    public string Uri { get; set; } = "amqp://guest:guest@localhost:5672";

    /// <summary>Topic exchange every service publishes to (ADR 0011).</summary>
    public string Exchange { get; set; } = "batchyard.events";

    /// <summary>How often the outbox dispatcher polls when there is nothing to send.</summary>
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromMilliseconds(500);

    public int BatchSize { get; set; } = 50;
}

public interface IMessagePublisher
{
    /// <summary>Returns only once the broker has confirmed the message.</summary>
    Task PublishAsync(IntegrationMessage message, CancellationToken ct);
}

public interface IMessageHandler
{
    /// <summary>Must be safe to call twice with the same message: delivery is at least once.</summary>
    Task HandleAsync(IntegrationMessage message, CancellationToken ct);
}
