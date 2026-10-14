using BenchmarkDotNet.Attributes;
using Ingest.Data;
using Microsoft.Data.SqlClient;
using Testcontainers.MsSql;

namespace Ingest.Benchmarks;

/// <summary>
/// One-by-one (N stored procedure calls) vs batch (one TVP + MERGE call), against real SQL Server in Docker.
/// Each iteration writes a fresh tenant, so both paths measure inserts of N new items.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(launchCount: 1, warmupCount: 2, iterationCount: 8, invocationCount: 1)]
public class UpsertBenchmarks
{
    private MsSqlContainer _container = null!;
    private SqlItemRepository _repository = null!;
    private List<ItemRow> _rows = [];
    private Guid _tenant;

    [Params(100, 1_000, 5_000)]
    public int Items { get; set; }

    [GlobalSetup]
    public async Task GlobalSetup()
    {
        _container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();
        await _container.StartAsync();
        var connectionString = new SqlConnectionStringBuilder(_container.GetConnectionString())
        {
            InitialCatalog = "batchyard",
        }.ConnectionString;
        DatabaseMigrator.Migrate(connectionString);
        _repository = new SqlItemRepository(connectionString);
        _rows = [.. Enumerable.Range(0, Items).Select(i => new ItemRow($"SKU-{i:D6}", $"Part {i}", 10m + i, "USD", i % 50))];
    }

    [IterationSetup]
    public void NewTenant() => _tenant = Guid.NewGuid();

    [Benchmark(Baseline = true, Description = "one-by-one")]
    public async Task OneByOne()
    {
        foreach (var row in _rows)
        {
            await _repository.UpsertAsync(_tenant, row);
        }
    }

    [Benchmark(Description = "batch (TVP + MERGE)")]
    public async Task Batch() => await _repository.UpsertBatchAsync(_tenant, _rows);

    [GlobalCleanup]
    public async Task GlobalCleanup() => await _container.DisposeAsync();
}
