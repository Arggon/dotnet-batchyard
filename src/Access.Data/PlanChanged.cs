using System.Text.Json;
using BuildingBlocks.Messaging;

namespace Access.Data;

/// <summary>Published when a tenant moves to another plan (ADR 0013).</summary>
public sealed record PlanChangedEvent(Guid TenantId, string OldPlan, string NewPlan, DateTime OccurredAt)
{
    public const string Type = "plan.changed";
}

/// <summary>
/// access.cache consumer: bumps the tenant's cache generation. Running it twice only costs one extra
/// cache miss, so it needs no inbox table.
/// </summary>
public sealed class PlanChangedHandler(CachedAccess cache) : IMessageHandler
{
    public static readonly QueueBinding Queue = new("access.cache", [PlanChangedEvent.Type]);

    public async Task HandleAsync(IntegrationMessage message, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(message);
        var evt = JsonSerializer.Deserialize<PlanChangedEvent>(message.Payload, JsonSerializerOptions.Web)
            ?? throw new InvalidOperationException($"Empty payload for {message.Id}.");
        await cache.InvalidateTenantAsync(evt.TenantId, ct);
    }
}
