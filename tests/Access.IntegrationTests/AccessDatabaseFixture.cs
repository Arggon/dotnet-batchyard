using Access.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Testcontainers.MsSql;

namespace Access.IntegrationTests;

public sealed class AccessDatabaseFixture : IAsyncLifetime
{
    private readonly MsSqlContainer _container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();

    public string ConnectionString { get; private set; } = "";

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        ConnectionString = new SqlConnectionStringBuilder(_container.GetConnectionString()) { InitialCatalog = "batchyard_access" }.ConnectionString;
        AccessDatabaseMigrator.Migrate(ConnectionString);
    }

    public AccessDbContext NewContext() =>
        new(new DbContextOptionsBuilder<AccessDbContext>().UseSqlServer(ConnectionString).Options);

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();
}

[CollectionDefinition(Name)]
public sealed class AccessDatabaseTests : ICollectionFixture<AccessDatabaseFixture>
{
    public const string Name = "access-db";
}
