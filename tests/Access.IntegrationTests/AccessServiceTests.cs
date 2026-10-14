using Access.Data;

namespace Access.IntegrationTests;

[Collection(AccessDatabaseTests.Name)]
public class AccessServiceTests(AccessDatabaseFixture db)
{
    private AccessService NewService() => new(db.NewContext(), TimeProvider.System);

    [Theory]
    [InlineData("Free", new[] { "viewer", "writer" }, new[] { "items:read", "items:write" }, null)]
    [InlineData("Pro", new[] { "batch-writer", "viewer", "writer" }, new[] { "ingest:batch", "items:read", "items:write" }, 5000)]
    [InlineData("Marketplace", new[] { "marketplace-seller", "viewer", "writer" },
        new[] { "exports:read", "ingest:batch", "items:read", "items:write" }, 1000)]
    public async Task New_client_gets_the_roles_its_plan_grants(string plan, string[] roles, string[] permissions, int? maxBatch)
    {
        var tenant = await NewService().CreateTenantAsync("Acme", plan);
        var client = await NewService().CreateClientAsync(tenant.Id, "ci", admin: false);

        var access = await NewService().ResolveAsync(client.ClientId);

        access.ShouldNotBeNull();
        access.TenantId.ShouldBe(tenant.Id);
        access.Plan.ShouldBe(plan);
        access.Roles.ShouldBe(roles);
        access.Permissions.ShouldBe(permissions);
        access.MaxBatchSize.ShouldBe(maxBatch);
    }

    [Fact]
    public async Task Admin_client_also_gets_plan_manage()
    {
        var tenant = await NewService().CreateTenantAsync("Acme", "Free");
        var admin = await NewService().CreateClientAsync(tenant.Id, "admin", admin: true);

        var access = await NewService().ResolveAsync(admin.ClientId);

        access!.Has("plan:manage").ShouldBeTrue();
        access.Roles.ShouldContain("tenant-admin");
    }

    [Fact]
    public async Task Changing_the_plan_swaps_plan_roles_and_keeps_manual_ones()
    {
        var tenant = await NewService().CreateTenantAsync("Acme", "Pro");
        var admin = await NewService().CreateClientAsync(tenant.Id, "admin", admin: true);
        var worker = await NewService().CreateClientAsync(tenant.Id, "worker", admin: false);

        (await NewService().ChangePlanAsync(tenant.Id, "Marketplace")).ShouldBeTrue();

        var adminAccess = await NewService().ResolveAsync(admin.ClientId);
        adminAccess!.Plan.ShouldBe("Marketplace");
        adminAccess.Roles.ShouldBe(["marketplace-seller", "tenant-admin", "viewer", "writer"]);
        adminAccess.MaxBatchSize.ShouldBe(1000);

        var workerAccess = await NewService().ResolveAsync(worker.ClientId);
        workerAccess!.Roles.ShouldNotContain("batch-writer");
        workerAccess.Has("exports:read").ShouldBeTrue();
    }

    [Fact]
    public async Task Downgrading_to_free_removes_batch_rights()
    {
        var tenant = await NewService().CreateTenantAsync("Acme", "Pro");
        var client = await NewService().CreateClientAsync(tenant.Id, "ci", admin: false);

        await NewService().ChangePlanAsync(tenant.Id, "Free");

        var access = await NewService().ResolveAsync(client.ClientId);
        access!.Has("ingest:batch").ShouldBeFalse();
        access.MaxBatchSize.ShouldBeNull();
    }

    [Fact]
    public async Task Unknown_plan_or_tenant_is_refused()
    {
        var tenant = await NewService().CreateTenantAsync("Acme", "Free");

        (await NewService().ChangePlanAsync(tenant.Id, "Platinum")).ShouldBeFalse();
        (await NewService().ChangePlanAsync(Guid.NewGuid(), "Pro")).ShouldBeFalse();
    }

    [Fact]
    public async Task Authenticate_checks_the_secret()
    {
        var tenant = await NewService().CreateTenantAsync("Acme", "Free");
        var client = await NewService().CreateClientAsync(tenant.Id, "ci", admin: false);

        (await NewService().AuthenticateAsync(client.ClientId, client.ClientSecret)).ShouldNotBeNull();
        (await NewService().AuthenticateAsync(client.ClientId, "wrong")).ShouldBeNull();
        (await NewService().AuthenticateAsync("cli_unknown", client.ClientSecret)).ShouldBeNull();
    }

    [Fact]
    public async Task Secrets_are_stored_hashed()
    {
        var tenant = await NewService().CreateTenantAsync("Acme", "Free");
        var client = await NewService().CreateClientAsync(tenant.Id, "ci", admin: false);

        await using var context = db.NewContext();
        var stored = context.ApiClients.Single(c => c.Id == client.ClientId).SecretHash;
        stored.ShouldStartWith("pbkdf2-sha256$");
        stored.ShouldNotContain(client.ClientSecret);
    }
}
