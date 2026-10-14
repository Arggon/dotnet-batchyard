using System.Data;
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
        Guid tenantId, IReadOnlyList<ItemRow> items, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(items);
        if (items.Count == 0)
        {
            return [];
        }

        using var table = ToTable(items);
        await using var connection = new SqlConnection(connectionString);
        var rows = await connection.QueryAsync<(int RowIndex, string Outcome)>(new CommandDefinition(
            "dbo.usp_UpsertItemsBatch",
            new { TenantId = tenantId, Rows = table.AsTableValuedParameter("dbo.ItemBatchRow") },
            commandType: CommandType.StoredProcedure,
            commandTimeout: 120,
            cancellationToken: cancellationToken));
        return [.. rows.Select(r => new BatchRowResult(r.RowIndex, ParseOutcome(r.Outcome)))];
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
