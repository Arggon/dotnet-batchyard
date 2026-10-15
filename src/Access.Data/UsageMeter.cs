using System.Text.Json;
using BuildingBlocks.Messaging;
using Microsoft.EntityFrameworkCore;

namespace Access.Data;

/// <summary>
/// The ingest.completed payload as Access reads it. Access keeps its own copy of the contract instead of
/// referencing Ingest.Data, so the services stay independently deployable (ADR 0018).
/// </summary>
public sealed record IngestCompleted(Guid TenantId, int Created, int Updated, int Rejected, DateTime OccurredAt)
{
    public const string Type = "ingest.completed";
}

/// <summary>One UTC day of a tenant's ingest usage.</summary>
public sealed record UsageDay(DateOnly Date, int Batches, int ItemsCreated, int ItemsUpdated, int ItemsRejected);

/// <summary>
/// access.usage consumer: adds each committed batch to the tenant's daily usage (ADR 0018).
/// Unlike plan.changed, running it twice would double-count, so the inbox insert and the counter
/// update share one transaction.
/// </summary>
public sealed class IngestUsageHandler(AccessDbContext db, TimeProvider clock) : IMessageHandler
{
    public static readonly QueueBinding Queue = new("access.usage", [IngestCompleted.Type]);

    public async Task HandleAsync(IntegrationMessage message, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(message);
        var evt = JsonSerializer.Deserialize<IngestCompleted>(message.Payload, JsonSerializerOptions.Web)
            ?? throw new InvalidOperationException($"Empty payload for {message.Id}.");
        var day = DateOnly.FromDateTime(evt.OccurredAt.ToUniversalTime());
        var now = clock.GetUtcNow().UtcDateTime;

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var fresh = await db.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO dbo.Inbox (MessageId, ProcessedAt)
            SELECT {message.Id}, {now} WHERE NOT EXISTS (SELECT 1 FROM dbo.Inbox WITH (UPDLOCK, HOLDLOCK) WHERE MessageId = {message.Id})
            """, ct);
        if (fresh == 0)
        {
            return; // already counted
        }

        await db.Database.ExecuteSqlInterpolatedAsync(
            $"""
            MERGE dbo.TenantUsage WITH (HOLDLOCK) AS t
            USING (SELECT {evt.TenantId} AS TenantId, {day.ToDateTime(TimeOnly.MinValue)} AS UsageDate) AS s
                ON t.TenantId = s.TenantId AND t.UsageDate = CAST(s.UsageDate AS DATE)
            WHEN MATCHED THEN UPDATE SET
                Batches = t.Batches + 1,
                ItemsCreated = t.ItemsCreated + {evt.Created},
                ItemsUpdated = t.ItemsUpdated + {evt.Updated},
                ItemsRejected = t.ItemsRejected + {evt.Rejected}
            WHEN NOT MATCHED THEN INSERT (TenantId, UsageDate, Batches, ItemsCreated, ItemsUpdated, ItemsRejected)
                VALUES (s.TenantId, CAST(s.UsageDate AS DATE), 1, {evt.Created}, {evt.Updated}, {evt.Rejected});
            """, ct);
        await tx.CommitAsync(ct);
    }
}

/// <summary>Reads the usage the consumer recorded.</summary>
public sealed class UsageQueries(AccessDbContext db)
{
    public async Task<IReadOnlyList<UsageDay>> LastDaysAsync(Guid tenantId, DateOnly today, int days, CancellationToken ct)
    {
        var from = today.AddDays(1 - days).ToDateTime(TimeOnly.MinValue);
        var rows = await db.Database.SqlQuery<UsageRow>(
            $"""
            SELECT CAST(UsageDate AS DATETIME2) AS UsageDate, Batches, ItemsCreated, ItemsUpdated, ItemsRejected
            FROM dbo.TenantUsage WHERE TenantId = {tenantId} AND UsageDate >= CAST({from} AS DATE)
            """).ToListAsync(ct);
        return rows
            .OrderByDescending(r => r.UsageDate)
            .Select(r => new UsageDay(DateOnly.FromDateTime(r.UsageDate), r.Batches, r.ItemsCreated, r.ItemsUpdated, r.ItemsRejected))
            .ToList();
    }

    // A settable class: EF's SqlQuery binds columns to properties by name.
    private sealed class UsageRow
    {
        public DateTime UsageDate { get; set; }
        public int Batches { get; set; }
        public int ItemsCreated { get; set; }
        public int ItemsUpdated { get; set; }
        public int ItemsRejected { get; set; }
    }
}
