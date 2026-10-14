using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Hosting;

namespace Gateway.Tests;

/// <summary>Four tiny Kestrel apps stand in for the services; each echoes its name and the path it got.</summary>
public sealed class FakeBackends : IAsyncLifetime
{
    private readonly List<WebApplication> _apps = [];

    public Dictionary<string, string> Addresses { get; } = [];

    public async Task InitializeAsync()
    {
        foreach (var name in new[] { "ingest", "access", "payments", "legacy" })
        {
            var builder = WebApplication.CreateSlimBuilder();
            builder.WebHost.UseUrls("http://127.0.0.1:0");
            var app = builder.Build();
            app.Run(async http =>
            {
                http.Response.Headers["X-Saw-Authorization"] = http.Request.Headers.Authorization.ToString();
                await http.Response.WriteAsync($"{name} {http.Request.Method} {http.Request.Path}");
            });
            await app.StartAsync();
            _apps.Add(app);
            Addresses[name] = app.Urls.Single() + "/";
        }
    }

    public async Task DisposeAsync()
    {
        foreach (var app in _apps)
        {
            await app.DisposeAsync();
        }
    }
}

public sealed class RoutingTests(FakeBackends backends) : IClassFixture<FakeBackends>
{
    private WebApplicationFactory<Program> Gateway(params (string Key, string Value)[] overrides) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(host =>
        {
            foreach (var (name, address) in backends.Addresses)
            {
                host.UseSetting($"ReverseProxy:Clusters:{name}:Destinations:d1:Address", address);
            }

            foreach (var (key, value) in overrides)
            {
                host.UseSetting(key, value);
            }
        });

    private static async Task<(HttpStatusCode Status, string? Backend, string Body)> Send(HttpClient http, HttpMethod method, string path)
    {
        using var request = new HttpRequestMessage(method, path);
        var response = await http.SendAsync(request);
        return (response.StatusCode, response.Headers.TryGetValues("X-Backend", out var v) ? v.Single() : null, await response.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("POST", "/v1/items:batch", "ingest")]
    [InlineData("POST", "/v1/items", "ingest")]
    [InlineData("GET", "/v1/items/BRK-001", "ingest")]
    [InlineData("GET", "/v1/reports/stock", "legacy")]
    [InlineData("POST", "/v1/auth/token", "access")]
    [InlineData("PUT", "/v1/tenants/me/plan", "access")]
    [InlineData("POST", "/v1/webhooks/marketplace", "access")]
    [InlineData("GET", "/v1/exports/items", "access")]
    [InlineData("POST", "/v1/invoices/7f1c/capture", "payments")]
    [InlineData("GET", "/v1/payments", "payments")]
    public async Task Default_routes_point_at_the_migrated_services(string method, string path, string backend)
    {
        await using var gateway = Gateway();
        using var http = gateway.CreateClient();

        var (status, header, body) = await Send(http, new HttpMethod(method), path);

        status.ShouldBe(HttpStatusCode.OK);
        header.ShouldBe(backend);
        body.ShouldBe($"{backend} {method} {path}");
    }

    [Fact]
    public async Task Flipping_one_cluster_id_moves_only_that_route_back_to_the_monolith()
    {
        await using var gateway = Gateway(("ReverseProxy:Routes:items-batch:ClusterId", "legacy"));
        using var http = gateway.CreateClient();

        (await Send(http, HttpMethod.Post, "/v1/items:batch")).Backend.ShouldBe("legacy");
        (await Send(http, HttpMethod.Get, "/v1/items/BRK-001")).Backend.ShouldBe("ingest");
    }

    [Fact]
    public async Task Authorization_header_is_forwarded_untouched()
    {
        await using var gateway = Gateway();
        using var http = gateway.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/v1/items/X");
        request.Headers.Add("Authorization", "Bearer abc.def.ghi");

        var response = await http.SendAsync(request);

        response.Headers.GetValues("X-Saw-Authorization").Single().ShouldBe("Bearer abc.def.ghi");
    }

    [Fact]
    public async Task Unknown_paths_are_404_at_the_gateway()
    {
        await using var gateway = Gateway();
        using var http = gateway.CreateClient();

        var (status, backend, _) = await Send(http, HttpMethod.Get, "/v1/nothing-here");

        status.ShouldBe(HttpStatusCode.NotFound);
        backend.ShouldBeNull();
    }
}
