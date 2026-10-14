using System.Diagnostics;

namespace BuildingBlocks.Messaging;

/// <summary>
/// An integration event. <see cref="Type"/> doubles as the RabbitMQ routing key. <see cref="TraceParent"/> is the
/// W3C trace context of the request that caused it, so one trace runs from that request to every consumer (ADR 0016).
/// </summary>
public sealed record IntegrationMessage(Guid Id, string Type, string Payload, string? TraceParent = null);

/// <summary>The ActivitySource for publish and consume spans. Plain System.Diagnostics; no OpenTelemetry dependency here.</summary>
public static class MessagingTelemetry
{
    public const string SourceName = "Batchyard.Messaging";
    public const string TraceParentHeader = "traceparent";

    public static readonly ActivitySource Source = new(SourceName);

    /// <summary>The current trace context to store with an outbox row (null when nothing is tracing).</summary>
    public static string? CurrentTraceParent => Activity.Current?.Id;

    internal static Activity? Start(string name, ActivityKind kind, string? traceParent, IntegrationMessage message)
    {
        ActivityContext.TryParse(traceParent, null, out var parent);
        var activity = Source.StartActivity(name, kind, parent);
        activity?.SetTag("messaging.system", "rabbitmq");
        activity?.SetTag("messaging.destination.name", message.Type);
        activity?.SetTag("messaging.message.id", message.Id.ToString());
        return activity;
    }
}

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
