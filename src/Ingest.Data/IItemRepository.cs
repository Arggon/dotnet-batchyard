namespace Ingest.Data;

public interface IItemRepository
{
    /// <summary>One-by-one path: one database round trip per item.</summary>
    Task<UpsertOutcome> UpsertAsync(Guid tenantId, ItemRow item, CancellationToken cancellationToken = default);

    /// <summary>
    /// Batch path: one round trip and one transaction for the whole list (TVP + MERGE). The same transaction
    /// writes an ingest.completed event to the outbox (ADR 0017); <paramref name="rejectedBeforeDb"/> is the number
    /// of rows the caller already rejected in validation, so the event can report the whole request.
    /// </summary>
    Task<IReadOnlyList<BatchRowResult>> UpsertBatchAsync(
        Guid tenantId, IReadOnlyList<ItemRow> items, int rejectedBeforeDb = 0, CancellationToken cancellationToken = default);

    Task<ItemRow?> FindAsync(Guid tenantId, string sku, CancellationToken cancellationToken = default);
}
