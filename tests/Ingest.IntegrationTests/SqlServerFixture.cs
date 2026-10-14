using Ingest.Data;
using Testcontainers.MsSql;

namespace Ingest.IntegrationTests;

/// <summary>One real SQL Server container per test run, migrated once. Tests isolate data by tenant id.</summary>
public sealed class SqlServerFixture : IAsyncLifetime
{
    private readonly MsSqlContainer _container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();

    public string ConnectionString { get; private set; } = "";

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        var builder = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(_container.GetConnectionString())
        {
            InitialCatalog = "batchyard",
        };
        ConnectionString = builder.ConnectionString;
        DatabaseMigrator.Migrate(ConnectionString);
    }

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();
}

[CollectionDefinition(Name)]
public sealed class SqlServerTests : ICollectionFixture<SqlServerFixture>
{
    public const string Name = "sql-server";
}
