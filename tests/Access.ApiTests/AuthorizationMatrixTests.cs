using System.Net;
using System.Net.Http.Json;

namespace Access.ApiTests;

/// <summary>Role x endpoint -> expected status. Roles come from the plan (and tenant-admin for admin clients).</summary>
[Collection(AccessApiTests.Name)]
public class AuthorizationMatrixTests(AccessApiFixture api)
{
    public static TheoryData<string, bool, string, HttpStatusCode> Matrix => new()
    {
        // plan, admin client, endpoint, expected
        { "Free", false, "GET /v1/tenants/me", HttpStatusCode.OK },
        { "Free", false, "PUT /v1/tenants/me/plan", HttpStatusCode.Forbidden },
        { "Free", false, "GET /v1/exports/items", HttpStatusCode.Forbidden },
        { "Free", true, "PUT /v1/tenants/me/plan", HttpStatusCode.OK },
        { "Pro", false, "GET /v1/tenants/me", HttpStatusCode.OK },
        { "Pro", false, "PUT /v1/tenants/me/plan", HttpStatusCode.Forbidden },
        { "Pro", false, "GET /v1/exports/items", HttpStatusCode.Forbidden },
        { "Pro", true, "GET /v1/exports/items", HttpStatusCode.Forbidden },
        { "Marketplace", false, "GET /v1/tenants/me", HttpStatusCode.OK },
        { "Marketplace", false, "PUT /v1/tenants/me/plan", HttpStatusCode.Forbidden },
        { "Marketplace", false, "GET /v1/exports/items", HttpStatusCode.OK },
        { "Marketplace", true, "PUT /v1/tenants/me/plan", HttpStatusCode.OK },
    };

    [Theory]
    [MemberData(nameof(Matrix))]
    public async Task Endpoint_answers_according_to_the_callers_roles(string plan, bool admin, string endpoint, HttpStatusCode expected)
    {
        using var http = await api.AuthorizedClientAsync(plan, admin);

        using var response = await Call(http, endpoint);

        response.StatusCode.ShouldBe(expected);
    }

    [Theory]
    [InlineData("GET /v1/tenants/me")]
    [InlineData("PUT /v1/tenants/me/plan")]
    [InlineData("GET /v1/exports/items")]
    public async Task Endpoint_without_a_token_returns_401(string endpoint)
    {
        using var http = api.Factory.CreateClient();

        using var response = await Call(http, endpoint);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Forged_token_returns_401()
    {
        using var http = api.Factory.CreateClient();
        http.DefaultRequestHeaders.Authorization = new("Bearer", "eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiJ4In0.invalid");

        (await http.GetAsync(new Uri("/v1/tenants/me", UriKind.Relative))).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    private static Task<HttpResponseMessage> Call(HttpClient http, string endpoint) => endpoint switch
    {
        "GET /v1/tenants/me" => http.GetAsync(new Uri("/v1/tenants/me", UriKind.Relative)),
        "PUT /v1/tenants/me/plan" => http.PutAsJsonAsync("/v1/tenants/me/plan", new { plan = "Pro" }),
        "GET /v1/exports/items" => http.GetAsync(new Uri("/v1/exports/items", UriKind.Relative)),
        _ => throw new ArgumentOutOfRangeException(nameof(endpoint)),
    };
}
