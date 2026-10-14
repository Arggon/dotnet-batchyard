namespace Ingest.Data;

public interface IItemRepository
{
    /// <summary>One-by-one path: one database round trip per item.</summary>
    Task<UpsertOutcome> UpsertAsync(Guid tenantId, ItemRow item, CancellationToken cancellationToken = default);

    /// <summary>Batch path: one round trip and one transaction for the whole list (TVP + MERGE).</summary>
    Task<IReadOnlyList<BatchRowResult>> UpsertBatchAsync(
        Guid tenantId, IReadOnlyList<ItemRow> items, CancellationToken cancellationToken = default);

    Task<ItemRow?> FindAsync(Guid tenantId, string sku, CancellationToken cancellationToken = default);
}
