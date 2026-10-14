using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BuildingBlocks.Auth;
using Ingest.Api.Items;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Ingest.IntegrationTests;

[Collection(SqlServerTests.Name)]
public sealed class ItemEndpointsTests : IDisposable
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public ItemEndpointsTests(SqlServerFixture db)
    {
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(host =>
        {
            host.UseSetting("ConnectionStrings:Ingest", db.ConnectionString);
            host.UseSetting("Messaging:Enabled", "false");
            host.UseSetting("Ingest:MaxBatchSize", "100");
            host.UseSetting("Jwt:SigningKey", TestTokens.SigningKey);
        });
        _client = _factory.CreateClient();
        _client.DefaultRequestHeaders.Authorization = new("Bearer",
            TestTokens.For(Guid.NewGuid(), ["items:read", "items:write", "ingest:batch"], maxBatch: null));
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    private static object Item(string sku, decimal price = 10m) =>
        new { sku, name = $"Part {sku}", price, currency = "USD", quantity = 5 };

    [Fact]
    public async Task Single_item_is_created_then_updated()
    {
        var created = await _client.PostAsJsonAsync("/v1/items", Item("ONE"));
        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("outcome").GetString().ShouldBe("created");

        var updated = await _client.PostAsJsonAsync("/v1/items", Item("ONE", price: 11m));
        updated.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await updated.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("outcome").GetString().ShouldBe("updated");
    }

    [Fact]
    public async Task Invalid_single_item_returns_problem_details()
    {
        var response = await _client.PostAsJsonAsync("/v1/items", new { sku = "", name = "x", price = 1, currency = "USD", quantity = 1 });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/problem+json");
    }

    [Fact]
    public async Task Missing_token_returns_401()
    {
        using var anonymous = _factory.CreateClient();

        var response = await anonymous.PostAsJsonAsync("/v1/items", Item("X"));

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Tenants_cannot_see_each_others_items()
    {
        await _client.PostAsJsonAsync("/v1/items", Item("PRIVATE"));
        using var other = _factory.CreateClient();
        other.DefaultRequestHeaders.Authorization = new("Bearer", TestTokens.For(Guid.NewGuid(), ["items:read"], null));

        (await other.GetAsync(new Uri("/v1/items/PRIVATE", UriKind.Relative))).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Token_batch_limit_is_lower_than_the_service_limit()
    {
        using var limited = _factory.CreateClient();
        limited.DefaultRequestHeaders.Authorization = new("Bearer",
            TestTokens.For(Guid.NewGuid(), ["ingest:batch"], maxBatch: 2));

        var response = await limited.PostAsJsonAsync("/v1/items:batch", new { items = new[] { Item("A"), Item("B"), Item("C") } });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).ShouldContain("between 1 and 2");
    }

    [Fact]
    public async Task Batch_commits_valid_rows_and_rejects_invalid_ones_in_input_order()
    {
        await _client.PostAsJsonAsync("/v1/items", Item("EXISTS"));
        var batch = new
        {
            items = new object[]
            {
                Item("NEW-1"),
                new { sku = "BAD", name = "Bad price", price = -1, currency = "USD", quantity = 1 },
                Item("EXISTS", price: 50m),
                Item("NEW-1"),
            },
        };

        var response = await _client.PostAsJsonAsync("/v1/items:batch", batch);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = (await response.Content.ReadFromJsonAsync<BatchResponse>())!;
        body.Created.ShouldBe(1);
        body.Updated.ShouldBe(1);
        body.Rejected.ShouldBe(2);
        body.Results.Select(r => (r.Index, r.Outcome, r.Reason)).ShouldBe(
        [
            (0, ItemOutcome.Created, null),
            (1, ItemOutcome.Rejected, "price must be zero or more"),
            (2, ItemOutcome.Updated, null),
            (3, ItemOutcome.Rejected, "duplicate sku in batch"),
        ]);

        var stored = await _client.GetFromJsonAsync<JsonElement>("/v1/items/EXISTS");
        stored.GetProperty("price").GetDecimal().ShouldBe(50m);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public async Task Empty_or_oversized_batch_returns_400(int size)
    {
        var batch = new { items = Enumerable.Range(0, size).Select(i => Item($"S{i}")).ToArray() };

        var response = await _client.PostAsJsonAsync("/v1/items:batch", batch);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).ShouldContain("between 1 and 100");
    }

    [Fact]
    public async Task Same_idempotency_key_and_body_replays_the_first_response()
    {
        var batch = new { items = new[] { Item("IDEM-1"), Item("IDEM-2") } };

        var first = await PostWithKey("/v1/items:batch", batch, "key-1");
        var replay = await PostWithKey("/v1/items:batch", batch, "key-1");

        replay.StatusCode.ShouldBe(HttpStatusCode.OK);
        replay.Headers.GetValues("Idempotent-Replayed").ShouldBe(["true"]);
        (await replay.Content.ReadAsStringAsync()).ShouldBe(await first.Content.ReadAsStringAsync());
        (await replay.Content.ReadFromJsonAsync<BatchResponse>())!.Created.ShouldBe(2);
    }

    [Fact]
    public async Task Same_idempotency_key_with_a_different_body_returns_409()
    {
        await PostWithKey("/v1/items", Item("K"), "key-2");

        var conflict = await PostWithKey("/v1/items", Item("K", price: 99m), "key-2");

        conflict.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    private async Task<HttpResponseMessage> PostWithKey(string url, object body, string key)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(body) };
        request.Headers.Add("Idempotency-Key", key);
        return await _client.SendAsync(request);
    }
}
