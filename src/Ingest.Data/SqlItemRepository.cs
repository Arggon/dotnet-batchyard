using System.Data;
using System.Diagnostics;
using System.Text.Json;
using Dapper;
using Microsoft.Data.SqlClient;

namespace Ingest.Data;

/// <summary>Dapper over stored procedures. No SQL is built from input; everything is a parameter or a TVP.</summary>
public sealed class SqlItemRepository(string connectionString) : IItemRepository
{
    public async Task<UpsertOutcome> UpsertAsync(
        Guid tenantId, ItemRow item, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        await using var connection = new SqlConnection(connectionString);
        var outcome = await connection.QuerySingleAsync<string>(new CommandDefinition(
            "dbo.usp_UpsertItem",
            new
            {
                TenantId = tenantId,
                item.Sku,
                item.Name,
                item.Price,
                item.Currency,
                item.Quantity,
            },
            commandType: CommandType.StoredProcedure,
            cancellationToken: cancellationToken));
        return ParseOutcome(outcome);
    }

    public async Task<IReadOnlyList<BatchRowResult>> UpsertBatchAsync(
        Guid tenantId, IReadOnlyList<ItemRow> items, int rejectedBeforeDb = 0, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(items);
        if (items.Count == 0)
        {
            return [];
        }

        using var table = ToTable(items);
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var tx = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);
        var rows = await connection.QueryAsync<(int RowIndex, string Outcome)>(new CommandDefinition(
            "dbo.usp_UpsertItemsBatch",
            new { TenantId = tenantId, Rows = table.AsTableValuedParameter("dbo.ItemBatchRow") },
            tx,
            commandType: CommandType.StoredProcedure,
            commandTimeout: 120,
            cancellationToken: cancellationToken));
        BatchRowResult[] results = [.. rows.Select(r => new BatchRowResult(r.RowIndex, ParseOutcome(r.Outcome)))];

        // ingest.completed rides in the same transaction as the upsert: no event for a rolled-back batch (ADR 0017).
        var now = DateTime.UtcNow;
        var evt = new IngestCompletedEvent(
            tenantId,
            results.Count(r => r.Outcome == UpsertOutcome.Created),
            results.Count(r => r.Outcome == UpsertOutcome.Updated),
            rejectedBeforeDb + results.Count(r => r.Outcome == UpsertOutcome.DuplicateInBatch),
            now);
        await connection.ExecuteAsync(new CommandDefinition(
            "INSERT INTO dbo.Outbox (Id, Type, Payload, OccurredAt, TraceParent) VALUES (@Id, @Type, @Payload, @now, @TraceParent)",
            new
            {
                Id = Guid.NewGuid(),
                Type = IngestCompletedEvent.Type,
                Payload = JsonSerializer.Serialize(evt, JsonSerializerOptions.Web),
                now,
                TraceParent = Activity.Current?.Id,
            },
            tx,
            cancellationToken: cancellationToken));
        await tx.CommitAsync(cancellationToken);
        return results;
    }

    public async Task<ItemRow?> FindAsync(Guid tenantId, string sku, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(connectionString);
        return await connection.QuerySingleOrDefaultAsync<ItemRow>(new CommandDefinition(
            "SELECT Sku, Name, Price, Currency, Quantity FROM dbo.Items WHERE TenantId = @tenantId AND Sku = @sku",
            new { tenantId, sku },
            cancellationToken: cancellationToken));
    }

    private static DataTable ToTable(IReadOnlyList<ItemRow> items)
    {
        // Column order must match dbo.ItemBatchRow.
        var table = new DataTable { Locale = System.Globalization.CultureInfo.InvariantCulture };
        table.Columns.Add("RowIndex", typeof(int));
        table.Columns.Add("Sku", typeof(string));
        table.Columns.Add("Name", typeof(string));
        table.Columns.Add("Price", typeof(decimal));
        table.Columns.Add("Currency", typeof(string));
        table.Columns.Add("Quantity", typeof(int));
        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];
            table.Rows.Add(i, item.Sku, item.Name, item.Price, item.Currency, item.Quantity);
        }

        return table;
    }

    private static UpsertOutcome ParseOutcome(string outcome) => outcome switch
    {
        "created" => UpsertOutcome.Created,
        "updated" => UpsertOutcome.Updated,
        "duplicate" => UpsertOutcome.DuplicateInBatch,
        _ => throw new InvalidOperationException($"Unknown upsert outcome '{outcome}'."),
    };
}

/// <summary>Published after a committed batch: counts only, no item data (ADR 0017).</summary>
public sealed record IngestCompletedEvent(Guid TenantId, int Created, int Updated, int Rejected, DateTime OccurredAt)
{
    public const string Type = "ingest.completed";
}
