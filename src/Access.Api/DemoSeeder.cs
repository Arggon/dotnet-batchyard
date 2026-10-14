using Access.Data;
using Microsoft.EntityFrameworkCore;

namespace Access.Api;

/// <summary>Creates a demo tenant and admin client for local runs (docker compose). Does nothing if the client exists.</summary>
internal static class DemoSeeder
{
    public static async Task SeedAsync(IServiceProvider services, SeedOptions seed)
    {
        var db = services.GetRequiredService<AccessDbContext>();
        if (await db.ApiClients.AnyAsync(c => c.Id == seed.AdminClientId))
        {
            return;
        }

        var access = services.GetRequiredService<AccessService>();
        var tenant = await access.CreateTenantAsync(seed.TenantName, seed.Plan);
        await access.CreateClientAsync(tenant.Id, "demo admin", admin: true, seed.AdminClientId, seed.AdminClientSecret);
    }
}
