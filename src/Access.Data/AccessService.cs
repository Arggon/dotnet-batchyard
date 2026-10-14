using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace Access.Data;

/// <summary>What a client may do right now: the union of its roles' permissions and the largest batch any role allows.</summary>
public sealed record EffectiveAccess(
    string ClientId,
    Guid TenantId,
    string Plan,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Permissions,
    int? MaxBatchSize)
{
    public bool Has(string permission) => Permissions.Contains(permission, StringComparer.Ordinal);
}

public sealed record NewClient(string ClientId, string ClientSecret);

public sealed class AccessService(AccessDbContext db, TimeProvider clock)
{
    public const string TenantAdminRole = "tenant-admin";

    public async Task<Tenant> CreateTenantAsync(string name, string planCode, CancellationToken ct = default)
    {
        var plan = await db.Plans.SingleAsync(p => p.Code == planCode, ct);
        var tenant = new Tenant { Id = Guid.NewGuid(), Name = name, PlanId = plan.Id, CreatedAt = clock.GetUtcNow().UtcDateTime };
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync(ct);
        return tenant;
    }

    /// <summary>Creates an API client with the plan's roles (plus tenant-admin when <paramref name="admin"/>).</summary>
    public async Task<NewClient> CreateClientAsync(
        Guid tenantId, string name, bool admin, string? clientId = null, string? secret = null, CancellationToken ct = default)
    {
        var tenant = await db.Tenants.Include(t => t.Plan).ThenInclude(p => p.Roles).SingleAsync(t => t.Id == tenantId, ct);
        secret ??= SecretHasher.NewSecret();
        var client = new ApiClient
        {
            Id = clientId ?? $"cli_{Guid.NewGuid():N}",
            TenantId = tenantId,
            Name = name,
            SecretHash = SecretHasher.Hash(secret),
            CreatedAt = clock.GetUtcNow().UtcDateTime,
        };
        client.Assignments.AddRange(tenant.Plan.Roles.Select(r => new Assignment { RoleId = r.Id, Source = Assignment.FromPlan }));
        if (admin)
        {
            var adminRole = await db.Roles.SingleAsync(r => r.Code == TenantAdminRole, ct);
            client.Assignments.Add(new Assignment { RoleId = adminRole.Id, Source = Assignment.Manual });
        }

        db.ApiClients.Add(client);
        await db.SaveChangesAsync(ct);
        return new NewClient(client.Id, secret);
    }

    /// <summary>Checks client credentials; null when the client is unknown or the secret is wrong.</summary>
    public async Task<EffectiveAccess?> AuthenticateAsync(string clientId, string secret, CancellationToken ct = default)
    {
        var hash = await db.ApiClients.AsNoTracking().Where(c => c.Id == clientId).Select(c => c.SecretHash).SingleOrDefaultAsync(ct);
        if (hash is null || !SecretHasher.Verify(secret, hash))
        {
            return null;
        }

        return await ResolveAsync(clientId, ct);
    }

    public async Task<EffectiveAccess?> ResolveAsync(string clientId, CancellationToken ct = default)
    {
        var client = await db.ApiClients.AsNoTracking()
            .Where(c => c.Id == clientId)
            .Select(c => new { c.Id, c.TenantId, Plan = c.Tenant.Plan.Code })
            .SingleOrDefaultAsync(ct);
        if (client is null)
        {
            return null;
        }

        var roles = await db.Assignments.AsNoTracking()
            .Where(a => a.ClientId == clientId)
            .Select(a => new { a.Role.Code, a.Role.MaxBatchSize, Permissions = a.Role.Permissions.Select(p => p.Code).ToList() })
            .ToListAsync(ct);

        return new EffectiveAccess(
            client.Id,
            client.TenantId,
            client.Plan,
            [.. roles.Select(r => r.Code).Order(StringComparer.Ordinal)],
            [.. roles.SelectMany(r => r.Permissions).Distinct().Order(StringComparer.Ordinal)],
            roles.Max(r => r.MaxBatchSize));
    }

    /// <summary>
    /// Moves the tenant to another plan: every client loses the roles the old plan granted and gets the new plan's roles.
    /// Roles assigned by hand (tenant-admin) are kept. Returns false for an unknown tenant or plan.
    /// A plan.changed event is written to the outbox in the same SaveChanges (one transaction); the
    /// permission cache is cleared by its consumer, not here (ADR 0013).
    /// </summary>
    public async Task<bool> ChangePlanAsync(Guid tenantId, string planCode, CancellationToken ct = default)
    {
        var plan = await db.Plans.Include(p => p.Roles).SingleOrDefaultAsync(p => p.Code == planCode, ct);
        var tenant = await db.Tenants.Include(t => t.Plan).SingleOrDefaultAsync(t => t.Id == tenantId, ct);
        if (plan is null || tenant is null)
        {
            return false;
        }

        var oldPlan = tenant.Plan.Code;
        tenant.PlanId = plan.Id;
        tenant.Plan = plan;
        var clients = await db.ApiClients.Include(c => c.Assignments).Where(c => c.TenantId == tenantId).ToListAsync(ct);
        foreach (var client in clients)
        {
            client.Assignments.RemoveAll(a => a.Source == Assignment.FromPlan);
            var manual = client.Assignments.Select(a => a.RoleId).ToHashSet();
            client.Assignments.AddRange(plan.Roles
                .Where(r => !manual.Contains(r.Id))
                .Select(r => new Assignment { ClientId = client.Id, RoleId = r.Id, Source = Assignment.FromPlan }));
        }

        var now = clock.GetUtcNow().UtcDateTime;
        db.Outbox.Add(new OutboxRow
        {
            Id = Guid.NewGuid(),
            Type = PlanChangedEvent.Type,
            Payload = JsonSerializer.Serialize(new PlanChangedEvent(tenantId, oldPlan, plan.Code, now), JsonSerializerOptions.Web),
            OccurredAt = now,
            TraceParent = BuildingBlocks.Messaging.MessagingTelemetry.CurrentTraceParent,
        });

        await db.SaveChangesAsync(ct);
        return true;
    }
}
