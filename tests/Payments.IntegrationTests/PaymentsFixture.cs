using System.Net.Http.Headers;
using BuildingBlocks.Auth;
using Dapper;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Payments.Data.Gateway;
using Testcontainers.MsSql;

namespace Payments.IntegrationTests;

public sealed class PaymentsFixture : IAsyncLifetime
{
    public const string SigningKey = "test-only-signing-key-0123456789abcdef";

    private readonly MsSqlContainer _sql = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();

    public string ConnectionString { get; private set; } = "";
    public WebApplicationFactory<Program> Factory { get; private set; } = null!;
    public FakePaymentGateway Gateway => Factory.Services.GetRequiredService<FakePaymentGateway>();

    public async Task InitializeAsync()
    {
        await _sql.StartAsync();
        ConnectionString = new SqlConnectionStringBuilder(_sql.GetConnectionString()) { InitialCatalog = "batchyard_payments" }.ConnectionString;
        Factory = new WebApplicationFactory<Program>().WithWebHostBuilder(host =>
        {
            host.UseSetting("ConnectionStrings:Payments", ConnectionString);
            host.UseSetting("Jwt:SigningKey", SigningKey);
            host.UseSetting("Gateway:BaseDelay", "00:00:00.010");
            host.UseSetting("Gateway:AttemptTimeout", "00:00:00.500");
        });
        _ = Factory.Server;
    }

    public HttpClient ClientFor(Guid tenantId, params string[] permissions)
    {
        var http = Factory.CreateClient();
        var token = new JwtTokenIssuer(new JwtOptions { SigningKey = SigningKey }, TimeProvider.System)
            .Issue("test-client", tenantId, "Pro", ["tenant-admin"], permissions.Length > 0 ? permissions : ["payments:read", "payments:write"], null);
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
        return http;
    }

    public async Task<int> CountAsync(string sql, object args)
    {
        await using var connection = new SqlConnection(ConnectionString);
        return await connection.ExecuteScalarAsync<int>(sql, args);
    }

    public async Task DisposeAsync()
    {
        await Factory.DisposeAsync();
        await _sql.DisposeAsync();
    }
}

[CollectionDefinition(Name)]
public sealed class PaymentsTests : ICollectionFixture<PaymentsFixture>
{
    public const string Name = "payments";
}
