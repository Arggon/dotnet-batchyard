using Access.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Access.ApiTests;

/// <summary>Cache-aside against real Redis (ADR 0008).</summary>
[Collection(AccessApiTests.Name)]
public class PermissionCacheTests(AccessApiFixture api)
{
    [Fact]
    public async Task Second_lookup_is_served_from_redis()
    {
        var (tenantId, client) = await api.NewClientAsync("Pro", admin: false);
        (await ResolveAsync(tenantId, client.ClientId))!.Has("ingest:batch").ShouldBeTrue();

        // Change the database behind the cache's back: a cache hit still returns the old answer.
        await RemoveAssignmentsAsync(client.ClientId);

        (await ResolveAsync(tenantId, client.ClientId))!.Has("ingest:batch").ShouldBeTrue();
        await using var scope = api.Factory.Services.CreateAsyncScope();
        (await scope.ServiceProvider.GetRequiredService<AccessService>().ResolveAsync(client.ClientId))!
            .Permissions.ShouldBeEmpty();
    }

    [Fact]
    public async Task Plan_change_clears_the_tenants_entries_through_the_plan_changed_event()
    {
        var (tenantId, client) = await api.NewClientAsync("Pro", admin: false);
        (await ResolveAsync(tenantId, client.ClientId))!.MaxBatchSize.ShouldBe(5000);

        await using (var scope = api.Factory.Services.CreateAsyncScope())
        {
            (await scope.ServiceProvider.GetRequiredService<AccessService>().ChangePlanAsync(tenantId, "Marketplace")).ShouldBeTrue();
        }

        // Nothing touched Redis directly: the plan.changed event goes outbox -> RabbitMQ -> access.cache consumer.
        var after = await AccessApiFixture.EventuallyAsync(() => ResolveAsync(tenantId, client.ClientId), a => a!.Plan == "Marketplace");
        after!.Plan.ShouldBe("Marketplace");
        after.MaxBatchSize.ShouldBe(1000);
    }

    [Fact]
    public async Task Plan_change_writes_plan_changed_to_the_outbox_in_the_same_save()
    {
        var (tenantId, _) = await api.NewClientAsync("Free", admin: false);

        await using var scope = api.Factory.Services.CreateAsyncScope();
        (await scope.ServiceProvider.GetRequiredService<AccessService>().ChangePlanAsync(tenantId, "Pro")).ShouldBeTrue();

        var db = scope.ServiceProvider.GetRequiredService<AccessDbContext>();
        var row = await db.Outbox.SingleAsync(o => o.Type == "plan.changed" && o.Payload.Contains(tenantId.ToString()));
        row.Payload.ShouldContain("\"oldPlan\":\"Free\"");
        row.Payload.ShouldContain("\"newPlan\":\"Pro\"");
    }

    [Fact]
    public async Task Handling_plan_changed_twice_is_harmless()
    {
        var (tenantId, client) = await api.NewClientAsync("Pro", admin: false);
        var message = new BuildingBlocks.Messaging.IntegrationMessage(Guid.NewGuid(), "plan.changed",
            $"{{\"tenantId\":\"{tenantId}\",\"oldPlan\":\"Pro\",\"newPlan\":\"Pro\",\"occurredAt\":\"2026-10-14T00:00:00Z\"}}");

        await using var scope = api.Factory.Services.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetRequiredService<PlanChangedHandler>();
        await handler.HandleAsync(message, CancellationToken.None);
        await handler.HandleAsync(message, CancellationToken.None);

        (await ResolveAsync(tenantId, client.ClientId))!.Plan.ShouldBe("Pro");
    }

    [Fact]
    public async Task Lookups_fall_back_to_sql_when_redis_is_down()
    {
        var (tenantId, client) = await api.NewClientAsync("Free", admin: false);
        await using var scope = api.Factory.Services.CreateAsyncScope();
        var cached = new CachedAccess(
            scope.ServiceProvider.GetRequiredService<AccessService>(), new BrokenCache(), NullLogger<CachedAccess>.Instance);

        var access = await cached.ResolveAsync(tenantId, client.ClientId);

        access!.Plan.ShouldBe("Free");
        await cached.InvalidateTenantAsync(tenantId); // swallowed, logged
    }

    private async Task<EffectiveAccess?> ResolveAsync(Guid tenantId, string clientId)
    {
        await using var scope = api.Factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<CachedAccess>().ResolveAsync(tenantId, clientId);
    }

    private async Task RemoveAssignmentsAsync(string clientId)
    {
        await using var scope = api.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AccessDbContext>();
        await db.Assignments.Where(a => a.ClientId == clientId).ExecuteDeleteAsync();
    }

    private sealed class BrokenCache : IDistributedCache
    {
        public byte[]? Get(string key) => throw new InvalidOperationException("redis down");
        public Task<byte[]?> GetAsync(string key, CancellationToken token = default) => throw new InvalidOperationException("redis down");
        public void Refresh(string key) => throw new InvalidOperationException("redis down");
        public Task RefreshAsync(string key, CancellationToken token = default) => throw new InvalidOperationException("redis down");
        public void Remove(string key) => throw new InvalidOperationException("redis down");
        public Task RemoveAsync(string key, CancellationToken token = default) => throw new InvalidOperationException("redis down");
        public void Set(string key, byte[] value, DistributedCacheEntryOptions options) => throw new InvalidOperationException("redis down");
        public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default) =>
            throw new InvalidOperationException("redis down");
    }
}
