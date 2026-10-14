using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;

namespace Legacy.Monolith;

// The "before" (ADR 0015). Written the way the old app did it: plain ADO.NET, and one SELECT plus one
// INSERT or UPDATE per row. It keeps the same contract as ingest-svc so the gateway can switch between them.

public sealed record LegacyItem(string? Sku, string? Name, decimal? Price, string? Currency, int? Quantity);

public sealed record LegacyBatch(IReadOnlyList<LegacyItem>? Items);

public sealed record LegacyResult(
    int Index,
    string? Sku,
    string Outcome,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] string? Reason = null);

public sealed record LegacyBatchResponse(int Created, int Updated, int Rejected, IReadOnlyList<LegacyResult> Results);

public sealed record LegacyStoredItem(string Sku, string Name, decimal Price, string Currency, int Quantity, DateTime CreatedAt, DateTime UpdatedAt);

public sealed record StockLine(string Currency, int Items, long Units, decimal Value);

public static partial class LegacyRules
{
    /// <summary>Same rules and messages as ingest-svc, copied into the monolith (it never shared code).</summary>
    public static string? Check(LegacyItem? item) => item switch
    {
        null => "item is required",
        _ when string.IsNullOrWhiteSpace(item.Sku) => "sku is required",
        { Sku.Length: > 64 } => "sku must be 1-64 characters of letters, digits, '.', '_' or '-'",
        _ when !Sku().IsMatch(item.Sku!) => "sku must be 1-64 characters of letters, digits, '.', '_' or '-'",
        _ when string.IsNullOrWhiteSpace(item.Name) => "name is required",
        { Name.Length: > 200 } => "name must be at most 200 characters",
        { Price: null } or { Price: < 0 } => "price must be zero or more",
        _ when decimal.Round(item.Price!.Value, 4) != item.Price || item.Price > 999_999_999_999_999m => "price must fit decimal(19,4)",
        _ when item.Currency is null || !Currency().IsMatch(item.Currency) => "currency must be a 3-letter upper-case ISO 4217 code",
        { Quantity: null } or { Quantity: < 0 } => "quantity must be zero or more",
        _ => null,
    };

    [GeneratedRegex("^[A-Za-z0-9._-]+$")]
    private static partial Regex Sku();

    [GeneratedRegex("^[A-Z]{3}$")]
    private static partial Regex Currency();
}

public sealed class LegacyItemStore(string connectionString)
{
    /// <summary>Returns true when the item was created, false when it was updated.</summary>
    public async Task<bool> SaveAsync(Guid tenantId, LegacyItem item, CancellationToken ct)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(ct);
        await using var tx = (SqlTransaction)await connection.BeginTransactionAsync(ct);
        var created = await SaveRowAsync(connection, tx, tenantId, item, ct);
        await tx.CommitAsync(ct);
        return created;
    }

    /// <summary>The chatty loop: two round trips per row, one transaction held open for the whole batch.</summary>
    public async Task<LegacyBatchResponse> SaveBatchAsync(Guid tenantId, IReadOnlyList<LegacyItem?> items, CancellationToken ct)
    {
        var results = new List<LegacyResult>(items.Count);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(ct);
        await using var tx = (SqlTransaction)await connection.BeginTransactionAsync(ct);
        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];
            var error = LegacyRules.Check(item);
            if (error is not null)
            {
                results.Add(new LegacyResult(i, item?.Sku, "rejected", error));
                continue;
            }

            if (!seen.Add(item!.Sku!))
            {
                results.Add(new LegacyResult(i, item.Sku, "rejected", "duplicate sku in batch"));
                continue;
            }

            var created = await SaveRowAsync(connection, tx, tenantId, item, ct);
            results.Add(new LegacyResult(i, item.Sku, created ? "created" : "updated"));
        }

        await tx.CommitAsync(ct);
        return new LegacyBatchResponse(
            results.Count(r => r.Outcome == "created"),
            results.Count(r => r.Outcome == "updated"),
            results.Count(r => r.Outcome == "rejected"),
            results);
    }

    public async Task<LegacyStoredItem?> FindAsync(Guid tenantId, string sku, CancellationToken ct)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(ct);
        await using var cmd = new SqlCommand(
            "SELECT Sku, Name, Price, Currency, Quantity, CreatedAt, UpdatedAt FROM dbo.Items WHERE TenantId = @t AND Sku = @s", connection);
        cmd.Parameters.AddWithValue("@t", tenantId);
        cmd.Parameters.AddWithValue("@s", sku);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct)
            ? new LegacyStoredItem(reader.GetString(0), reader.GetString(1), reader.GetDecimal(2), reader.GetString(3),
                reader.GetInt32(4), reader.GetDateTime(5), reader.GetDateTime(6))
            : null;
    }

    /// <summary>Legacy-only feature that has not been migrated: stock value per currency.</summary>
    public async Task<IReadOnlyList<StockLine>> StockAsync(Guid tenantId, CancellationToken ct)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(ct);
        await using var cmd = new SqlCommand(
            """
            SELECT Currency, COUNT(*), CAST(SUM(CAST(Quantity AS BIGINT)) AS BIGINT), SUM(Price * Quantity)
            FROM dbo.Items WHERE TenantId = @t GROUP BY Currency ORDER BY Currency
            """, connection);
        cmd.Parameters.AddWithValue("@t", tenantId);
        var lines = new List<StockLine>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            lines.Add(new StockLine(reader.GetString(0), reader.GetInt32(1), reader.GetInt64(2), reader.GetDecimal(3)));
        }

        return lines;
    }

    private static async Task<bool> SaveRowAsync(SqlConnection connection, SqlTransaction tx, Guid tenantId, LegacyItem item, CancellationToken ct)
    {
        await using var exists = new SqlCommand(
            "SELECT COUNT(*) FROM dbo.Items WITH (UPDLOCK, HOLDLOCK) WHERE TenantId = @t AND Sku = @s", connection, tx);
        exists.Parameters.AddWithValue("@t", tenantId);
        exists.Parameters.AddWithValue("@s", item.Sku);
        var found = Convert.ToInt32(await exists.ExecuteScalarAsync(ct), CultureInfo.InvariantCulture) > 0;

        await using var write = new SqlCommand(found
            ? "UPDATE dbo.Items SET Name = @n, Price = @p, Currency = @c, Quantity = @q, UpdatedAt = SYSUTCDATETIME() WHERE TenantId = @t AND Sku = @s"
            : "INSERT INTO dbo.Items (TenantId, Sku, Name, Price, Currency, Quantity, CreatedAt, UpdatedAt) VALUES (@t, @s, @n, @p, @c, @q, SYSUTCDATETIME(), SYSUTCDATETIME())",
            connection, tx);
        write.Parameters.AddWithValue("@t", tenantId);
        write.Parameters.AddWithValue("@s", item.Sku);
        write.Parameters.AddWithValue("@n", item.Name!.Trim());
        write.Parameters.Add("@p", System.Data.SqlDbType.Decimal).Value = item.Price!.Value;
        write.Parameters["@p"].Precision = 19;
        write.Parameters["@p"].Scale = 4;
        write.Parameters.AddWithValue("@c", item.Currency);
        write.Parameters.AddWithValue("@q", item.Quantity!.Value);
        await write.ExecuteNonQueryAsync(ct);
        return !found;
    }
}
