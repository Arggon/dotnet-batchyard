using System.Net.Http.Headers;
using System.Net.Http.Json;
using Access.Api;
using Access.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.MsSql;
using Testcontainers.Redis;

namespace Access.ApiTests;

/// <summary>Real SQL Server + the Access API in memory. Each test creates its own tenant.</summary>
public sealed class AccessApiFixture : IAsyncLifetime
{
    public const string SigningKey = "test-only-signing-key-0123456789abcdef";
    public const string WebhookSecret = "test-webhook-secret";

    private readonly MsSqlContainer _container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();

    private readonly RedisContainer _redis = new RedisBuilder("redis:7.4-alpine").Build();

    public string ConnectionString { get; private set; } = "";
    public string RedisConnectionString { get; private set; } = "";
    public WebApplicationFactory<Program> Factory { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await Task.WhenAll(_container.StartAsync(), _redis.StartAsync());
        RedisConnectionString = _redis.GetConnectionString();
        ConnectionString = new SqlConnectionStringBuilder(_container.GetConnectionString()) { InitialCatalog = "batchyard_access" }.ConnectionString;
        Factory = CreateFactory(tokenRequestsPerMinute: 1000);
    }

    public WebApplicationFactory<Program> CreateFactory(int tokenRequestsPerMinute) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(host =>
        {
            host.UseSetting("ConnectionStrings:Access", ConnectionString);
            host.UseSetting("ConnectionStrings:Redis", RedisConnectionString);
            host.UseSetting("Jwt:SigningKey", SigningKey);
            host.UseSetting("Access:WebhookSecret", WebhookSecret);
            host.UseSetting("Access:TokenRequestsPerMinute", tokenRequestsPerMinute.ToString(System.Globalization.CultureInfo.InvariantCulture));
        });

    public async Task<(Guid TenantId, NewClient Client)> NewClientAsync(string plan, bool admin)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var access = scope.ServiceProvider.GetRequiredService<AccessService>();
        var tenant = await access.CreateTenantAsync("Test tenant", plan);
        return (tenant.Id, await access.CreateClientAsync(tenant.Id, "test", admin));
    }

    public async Task<HttpClient> AuthorizedClientAsync(string plan, bool admin)
    {
        var (_, client) = await NewClientAsync(plan, admin);
        var http = Factory.CreateClient();
        var token = await http.PostAsJsonAsync("/v1/auth/token", new { client.ClientId, client.ClientSecret });
        var body = await token.Content.ReadFromJsonAsync<TokenResponse>();
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body!.AccessToken);
        return http;
    }

    public async Task DisposeAsync()
    {
        await Factory.DisposeAsync();
        await _container.DisposeAsync();
        await _redis.DisposeAsync();
    }
}

[CollectionDefinition(Name)]
public sealed class AccessApiTests : ICollectionFixture<AccessApiFixture>
{
    public const string Name = "access-api";
}
