using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Access.Api;

namespace Access.ApiTests;

[Collection(AccessApiTests.Name)]
public class TokenAndPlanTests(AccessApiFixture api)
{
    [Fact]
    public async Task Valid_credentials_get_a_token_with_tenant_and_permissions()
    {
        var (tenantId, client) = await api.NewClientAsync("Pro", admin: false);
        using var http = api.Factory.CreateClient();

        var response = await http.PostAsJsonAsync("/v1/auth/token", new { client.ClientId, client.ClientSecret });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = (await response.Content.ReadFromJsonAsync<TokenResponse>())!;
        body.ExpiresIn.ShouldBe(900);
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(body.AccessToken);
        jwt.Claims.Single(c => c.Type == "tenant_id").Value.ShouldBe(tenantId.ToString());
        jwt.Claims.Where(c => c.Type == "perm").Select(c => c.Value).ShouldBe(["ingest:batch", "items:read", "items:write"], ignoreOrder: true);
        jwt.Claims.Single(c => c.Type == "max_batch").Value.ShouldBe("5000");
    }

    [Theory]
    [InlineData("wrong-secret")]
    [InlineData("")]
    public async Task Wrong_or_missing_secret_is_refused(string secret)
    {
        var (_, client) = await api.NewClientAsync("Free", admin: false);
        using var http = api.Factory.CreateClient();

        var response = await http.PostAsJsonAsync("/v1/auth/token", new { client.ClientId, clientSecret = secret });

        response.StatusCode.ShouldBeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Token_endpoint_is_rate_limited()
    {
        await using var limited = api.CreateFactory(tokenRequestsPerMinute: 2);
        using var http = limited.CreateClient();
        var request = new { clientId = "cli_nobody", clientSecret = "x" };

        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 3; i++)
        {
            statuses.Add((await http.PostAsJsonAsync("/v1/auth/token", request)).StatusCode);
        }

        statuses.ShouldBe([HttpStatusCode.Unauthorized, HttpStatusCode.Unauthorized, HttpStatusCode.TooManyRequests]);
    }

    [Fact]
    public async Task Plan_change_applies_inside_access_at_once()
    {
        using var http = await api.AuthorizedClientAsync("Pro", admin: true);

        (await http.PutAsJsonAsync("/v1/tenants/me/plan", new { plan = "Marketplace" })).StatusCode.ShouldBe(HttpStatusCode.OK);

        // Same (old) token: Access checks live assignments, so exports:read is granted now.
        (await http.GetAsync(new Uri("/v1/exports/items", UriKind.Relative))).StatusCode.ShouldBe(HttpStatusCode.OK);
        var me = await http.GetFromJsonAsync<JsonElement>("/v1/tenants/me");
        me.GetProperty("plan").GetString().ShouldBe("Marketplace");
        me.GetProperty("maxBatchSize").GetInt32().ShouldBe(1000);
    }

    [Fact]
    public async Task Unknown_plan_returns_400()
    {
        using var http = await api.AuthorizedClientAsync("Free", admin: true);

        (await http.PutAsJsonAsync("/v1/tenants/me/plan", new { plan = "Platinum" })).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Marketplace_webhook_grants_the_marketplace_seller_role()
    {
        var (tenantId, _) = await api.NewClientAsync("Free", admin: false);
        using var http = api.Factory.CreateClient();
        http.DefaultRequestHeaders.Add("X-Webhook-Secret", AccessApiFixture.WebhookSecret);

        var response = await http.PostAsJsonAsync("/v1/webhooks/marketplace", new { tenantId, @event = "subscribe" });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("plan").GetString().ShouldBe("Marketplace");
    }

    [Theory]
    [InlineData("wrong", "subscribe", HttpStatusCode.Unauthorized)]
    [InlineData(AccessApiFixture.WebhookSecret, "refund", HttpStatusCode.BadRequest)]
    public async Task Webhook_rejects_bad_secret_and_unknown_events(string secret, string evt, HttpStatusCode expected)
    {
        using var http = api.Factory.CreateClient();
        http.DefaultRequestHeaders.Add("X-Webhook-Secret", secret);

        var response = await http.PostAsJsonAsync("/v1/webhooks/marketplace", new { tenantId = Guid.NewGuid(), @event = evt });

        response.StatusCode.ShouldBe(expected);
    }
}
