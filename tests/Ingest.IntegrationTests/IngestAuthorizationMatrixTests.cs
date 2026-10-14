using System.Net;
using System.Net.Http.Json;
using Access.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Ingest.IntegrationTests;

/// <summary>
/// Role x ingest endpoint -> expected status. Each role's permissions and batch limit are read from the
/// real access catalog (db/access, migrated into the same SQL Server), so the matrix breaks if the catalog changes.
/// </summary>
[Collection(SqlServerTests.Name)]
public sealed class IngestAuthorizationMatrixTests : IDisposable
{
    private readonly SqlServerFixture _db;
    private readonly WebApplicationFactory<Program> _factory;

    public IngestAuthorizationMatrixTests(SqlServerFixture db)
    {
        _db = db;
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(host =>
        {
            host.UseSetting("ConnectionStrings:Ingest", db.ConnectionString);
            host.UseSetting("Messaging:Enabled", "false");
            host.UseSetting("Ingest:MaxBatchSize", "5000");
            host.UseSetting("Jwt:SigningKey", TestTokens.SigningKey);
        });
    }

    public void Dispose() => _factory.Dispose();

    public static TheoryData<string, string, HttpStatusCode> Matrix => new()
    {
        { "viewer", "GET item", HttpStatusCode.NotFound },
        { "viewer", "POST item", HttpStatusCode.Forbidden },
        { "viewer", "POST batch of 10", HttpStatusCode.Forbidden },
        { "writer", "GET item", HttpStatusCode.NotFound },
        { "writer", "POST item", HttpStatusCode.Created },
        { "writer", "POST batch of 10", HttpStatusCode.Forbidden },
        { "batch-writer", "POST item", HttpStatusCode.Created },
        { "batch-writer", "POST batch of 10", HttpStatusCode.OK },
        { "batch-writer", "POST batch of 1500", HttpStatusCode.OK },
        { "marketplace-seller", "POST item", HttpStatusCode.Created },
        { "marketplace-seller", "POST batch of 10", HttpStatusCode.OK },
        { "marketplace-seller", "POST batch of 1500", HttpStatusCode.BadRequest }, // role limit is 1,000
        { "tenant-admin", "GET item", HttpStatusCode.Forbidden },
        { "tenant-admin", "POST item", HttpStatusCode.Forbidden },
        { "tenant-admin", "POST batch of 10", HttpStatusCode.Forbidden },
    };

    [Theory]
    [MemberData(nameof(Matrix))]
    public async Task Ingest_endpoint_answers_according_to_the_role(string role, string endpoint, HttpStatusCode expected)
    {
        var (permissions, maxBatch) = await RoleFromCatalogAsync(role);
        using var http = _factory.CreateClient();
        http.DefaultRequestHeaders.Authorization = new("Bearer", TestTokens.For(Guid.NewGuid(), permissions, maxBatch, [role]));

        using var response = await Call(http, endpoint);

        response.StatusCode.ShouldBe(expected);
    }

    [Theory]
    [InlineData("GET item")]
    [InlineData("POST item")]
    [InlineData("POST batch of 10")]
    public async Task No_token_returns_401(string endpoint)
    {
        using var http = _factory.CreateClient();

        using var response = await Call(http, endpoint);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    private async Task<(List<string> Permissions, int? MaxBatch)> RoleFromCatalogAsync(string role)
    {
        var accessDb = new SqlConnectionStringBuilder(_db.ConnectionString) { InitialCatalog = "batchyard_access_catalog" }.ConnectionString;
        AccessDatabaseMigrator.Migrate(accessDb);
        await using var context = new AccessDbContext(new DbContextOptionsBuilder<AccessDbContext>().UseSqlServer(accessDb).Options);
        var found = await context.Roles.AsNoTracking()
            .Where(r => r.Code == role)
            .Select(r => new { r.MaxBatchSize, Permissions = r.Permissions.Select(p => p.Code).ToList() })
            .SingleAsync();
        return (found.Permissions, found.MaxBatchSize);
    }

    private static Task<HttpResponseMessage> Call(HttpClient http, string endpoint)
    {
        static object Item(string sku) => new { sku, name = "Part", price = 1m, currency = "USD", quantity = 1 };
        return endpoint switch
        {
            "GET item" => http.GetAsync(new Uri("/v1/items/NOPE", UriKind.Relative)),
            "POST item" => http.PostAsJsonAsync("/v1/items", Item("ONE")),
            "POST batch of 10" => http.PostAsJsonAsync("/v1/items:batch", new { items = Enumerable.Range(0, 10).Select(i => Item($"B{i}")) }),
            "POST batch of 1500" => http.PostAsJsonAsync("/v1/items:batch", new { items = Enumerable.Range(0, 1500).Select(i => Item($"L{i}")) }),
            _ => throw new ArgumentOutOfRangeException(nameof(endpoint)),
        };
    }
}
