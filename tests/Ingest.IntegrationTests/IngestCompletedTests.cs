using System.Text.Json;
using BuildingBlocks.Messaging;
using Dapper;
using Ingest.Data;
using Microsoft.Data.SqlClient;

namespace Ingest.IntegrationTests;

/// <summary>ingest.completed rides in the batch transaction (ADR 0017).</summary>
[Collection(SqlServerTests.Name)]
public class IngestCompletedTests(SqlServerFixture db)
{
    private readonly SqlItemRepository _repository = new(db.ConnectionString);
    private readonly Guid _tenant = Guid.NewGuid();

    private static ItemRow Row(string sku, decimal price = 10m) => new(sku, $"Part {sku}", price, "USD", 1);

    private async Task<List<string>> EventsForTenant()
    {
        await using var connection = new SqlConnection(db.ConnectionString);
        return (await connection.QueryAsync<string>(
            "SELECT Payload FROM dbo.Outbox WHERE Type = 'ingest.completed' AND JSON_VALUE(Payload, '$.tenantId') = @t",
            new { t = _tenant.ToString() })).AsList();
    }

    [Fact]
    public async Task A_committed_batch_writes_one_event_with_its_counts()
    {
        await _repository.UpsertAsync(_tenant, Row("E-2"));

        await _repository.UpsertBatchAsync(_tenant, [Row("E-1"), Row("E-2"), Row("E-1")], rejectedBeforeDb: 2);

        var evt = JsonSerializer.Deserialize<IngestCompletedEvent>((await EventsForTenant()).ShouldHaveSingleItem(), JsonSerializerOptions.Web)!;
        evt.Created.ShouldBe(1);
        evt.Updated.ShouldBe(1);
        evt.Rejected.ShouldBe(3); // 2 failed validation + 1 duplicate in the batch
    }

    [Fact]
    public async Task A_rolled_back_batch_writes_no_event()
    {
        // A negative price passes no API validation here, so the CHECK constraint fails inside the procedure.
        await Should.ThrowAsync<SqlException>(() => _repository.UpsertBatchAsync(_tenant, [Row("OK-1"), Row("BAD-1", price: -1m)]));

        (await EventsForTenant()).ShouldBeEmpty();
        (await _repository.FindAsync(_tenant, "OK-1")).ShouldBeNull();
    }

    [Fact]
    public async Task The_shared_dispatcher_publishes_the_event_with_its_routing_key()
    {
        await _repository.UpsertBatchAsync(_tenant, [Row("D-1")]);
        var published = new List<IntegrationMessage>();

        // Same dispatcher code the services run; the RabbitMQ side of it is covered by the payments and access tests.
        await new SqlOutboxStore(db.ConnectionString).DispatchBatchAsync(1000, m =>
        {
            published.Add(m);
            return Task.CompletedTask;
        }, CancellationToken.None);

        published.ShouldContain(m => m.Type == "ingest.completed" && m.Payload.Contains(_tenant.ToString()));
    }
}
