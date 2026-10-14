using Ingest.Data;

namespace Ingest.IntegrationTests;

[Collection(SqlServerTests.Name)]
public class ItemRepositoryTests(SqlServerFixture db)
{
    private readonly SqlItemRepository _repository = new(db.ConnectionString);
    private readonly Guid _tenant = Guid.NewGuid();

    private static ItemRow Row(string sku, decimal price = 10m, int quantity = 1) =>
        new(sku, $"Part {sku}", price, "USD", quantity);

    [Fact]
    public async Task Single_upsert_creates_then_updates()
    {
        (await _repository.UpsertAsync(_tenant, Row("SKU-1"))).ShouldBe(UpsertOutcome.Created);
        (await _repository.UpsertAsync(_tenant, Row("SKU-1", price: 12.5m))).ShouldBe(UpsertOutcome.Updated);

        var stored = await _repository.FindAsync(_tenant, "SKU-1");
        stored.ShouldNotBeNull();
        stored.Price.ShouldBe(12.5m);
    }

    [Fact]
    public async Task Batch_upsert_reports_created_and_updated_per_row_in_input_order()
    {
        await _repository.UpsertAsync(_tenant, Row("B"));

        var results = await _repository.UpsertBatchAsync(_tenant, [Row("A"), Row("B", price: 99m), Row("C")]);

        results.Select(r => (r.Index, r.Outcome)).ShouldBe(
        [
            (0, UpsertOutcome.Created),
            (1, UpsertOutcome.Updated),
            (2, UpsertOutcome.Created),
        ]);
        (await _repository.FindAsync(_tenant, "B"))!.Price.ShouldBe(99m);
    }

    [Fact]
    public async Task Duplicate_skus_inside_one_batch_keep_the_first_and_reject_the_rest()
    {
        var results = await _repository.UpsertBatchAsync(
            _tenant, [Row("DUP", price: 1m), Row("OTHER"), Row("DUP", price: 2m)]);

        results.Select(r => r.Outcome).ShouldBe(
            [UpsertOutcome.Created, UpsertOutcome.Created, UpsertOutcome.DuplicateInBatch]);
        (await _repository.FindAsync(_tenant, "DUP"))!.Price.ShouldBe(1m);
    }

    [Fact]
    public async Task Tenants_are_isolated()
    {
        var otherTenant = Guid.NewGuid();
        await _repository.UpsertBatchAsync(_tenant, [Row("SHARED")]);

        var results = await _repository.UpsertBatchAsync(otherTenant, [Row("SHARED")]);

        results.Single().Outcome.ShouldBe(UpsertOutcome.Created);
    }

    [Fact]
    public async Task Large_batch_of_5000_rows_commits_in_one_call()
    {
        var rows = Enumerable.Range(0, 5000).Select(i => Row($"BULK-{i:D5}", price: i)).ToList();

        var results = await _repository.UpsertBatchAsync(_tenant, rows);

        results.Count.ShouldBe(5000);
        results.ShouldAllBe(r => r.Outcome == UpsertOutcome.Created);
        results.Select(r => r.Index).ShouldBe(Enumerable.Range(0, 5000));
    }

    [Fact]
    public async Task Empty_batch_returns_no_results()
    {
        (await _repository.UpsertBatchAsync(_tenant, [])).ShouldBeEmpty();
    }
}
