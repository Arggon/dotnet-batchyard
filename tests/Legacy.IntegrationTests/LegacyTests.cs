using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using BuildingBlocks.Auth;
using Ingest.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Testcontainers.MsSql;

namespace Legacy.IntegrationTests;

/// <summary>The monolith against real SQL Server, with the schema created by ingest-svc's migrations (shared table).</summary>
public sealed class LegacyFixture : IAsyncLifetime
{
    public const string SigningKey = "test-only-signing-key-0123456789abcdef";
    private readonly MsSqlContainer _sql = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();

    public WebApplicationFactory<Program> Factory { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _sql.StartAsync();
        var cs = new SqlConnectionStringBuilder(_sql.GetConnectionString()) { InitialCatalog = "batchyard" }.ConnectionString;
        DatabaseMigrator.Migrate(cs);
        Factory = new WebApplicationFactory<Program>().WithWebHostBuilder(host =>
        {
            host.UseSetting("ConnectionStrings:Items", cs);
            host.UseSetting("Jwt:SigningKey", SigningKey);
        });
    }

    public HttpClient ClientFor(Guid tenant, int? maxBatch = 5000, params string[] permissions)
    {
        var http = Factory.CreateClient();
        var token = new JwtTokenIssuer(new JwtOptions { SigningKey = SigningKey }, TimeProvider.System)
            .Issue("test", tenant, "Pro", [], permissions.Length > 0 ? permissions : ["items:read", "items:write", "ingest:batch"], maxBatch);
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
        return http;
    }

    public async Task DisposeAsync()
    {
        await Factory.DisposeAsync();
        await _sql.DisposeAsync();
    }
}

public sealed class LegacyTests(LegacyFixture app) : IClassFixture<LegacyFixture>
{
    private readonly Guid _tenant = Guid.NewGuid();

    private static object Item(string sku, decimal price = 10m, int qty = 1) => new { sku, name = "Part " + sku, price, currency = "USD", quantity = qty };

    [Fact]
    public async Task Batch_returns_the_same_per_row_contract_as_ingest()
    {
        using var http = app.ClientFor(_tenant);

        var response = await http.PostAsJsonAsync("/v1/items:batch", new
        {
            items = new[] { Item("A-1"), Item("B-2", price: -1), Item("A-1"), Item("C-3") },
        });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("created").GetInt32().ShouldBe(2);
        body.GetProperty("rejected").GetInt32().ShouldBe(2);
        var results = body.GetProperty("results").EnumerateArray().Select(r => (
            r.GetProperty("index").GetInt32(), r.GetProperty("outcome").GetString(),
            r.TryGetProperty("reason", out var reason) ? reason.GetString() : null)).ToList();
        results.ShouldBe([
            (0, "created", null),
            (1, "rejected", "price must be zero or more"),
            (2, "rejected", "duplicate sku in batch"),
            (3, "created", null),
        ]);
    }

    [Fact]
    public async Task Second_batch_updates_existing_rows()
    {
        using var http = app.ClientFor(_tenant);
        await http.PostAsJsonAsync("/v1/items:batch", new { items = new[] { Item("U-1", 5m) } });

        var body = await (await http.PostAsJsonAsync("/v1/items:batch", new { items = new[] { Item("U-1", 7.5m) } }))
            .Content.ReadFromJsonAsync<JsonElement>();

        body.GetProperty("updated").GetInt32().ShouldBe(1);
        (await http.GetFromJsonAsync<JsonElement>("/v1/items/U-1")).GetProperty("price").GetDecimal().ShouldBe(7.5m);
    }

    [Fact]
    public async Task Single_item_write_returns_201_then_200()
    {
        using var http = app.ClientFor(_tenant);

        (await http.PostAsJsonAsync("/v1/items", Item("S-1"))).StatusCode.ShouldBe(HttpStatusCode.Created);
        (await http.PostAsJsonAsync("/v1/items", Item("S-1"))).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Batch_size_respects_the_token_limit()
    {
        using var http = app.ClientFor(_tenant, maxBatch: 2);

        var response = await http.PostAsJsonAsync("/v1/items:batch", new { items = new[] { Item("L-1"), Item("L-2"), Item("L-3") } });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Stock_report_stays_in_the_monolith()
    {
        using var http = app.ClientFor(_tenant);
        await http.PostAsJsonAsync("/v1/items:batch", new { items = new[] { Item("R-1", 2m, 10), Item("R-2", 1.5m, 4) } });

        var lines = await http.GetFromJsonAsync<JsonElement>("/v1/reports/stock");

        var usd = lines.EnumerateArray().Single(l => l.GetProperty("currency").GetString() == "USD");
        usd.GetProperty("units").GetInt64().ShouldBe(14);
        usd.GetProperty("value").GetDecimal().ShouldBe(26m);
    }

    [Theory]
    [InlineData("items:read", HttpStatusCode.Forbidden)]
    [InlineData("ingest:batch", HttpStatusCode.OK)]
    public async Task Batch_needs_ingest_batch(string permission, HttpStatusCode expected)
    {
        using var http = app.ClientFor(_tenant, 5000, permission);

        (await http.PostAsJsonAsync("/v1/items:batch", new { items = new[] { Item("P-" + permission) } })).StatusCode.ShouldBe(expected);
    }

    [Fact]
    public async Task No_token_is_401()
    {
        using var http = app.Factory.CreateClient();

        (await http.GetAsync(new Uri("/v1/reports/stock", UriKind.Relative))).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}
