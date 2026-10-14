namespace Ingest.Data;

/// <summary>An item as stored for a tenant. Money is a decimal plus an ISO 4217 currency code.</summary>
public sealed record ItemRow(string Sku, string Name, decimal Price, string Currency, int Quantity);

public enum UpsertOutcome
{
    Created,
    Updated,
    DuplicateInBatch,
}

/// <summary>Outcome for one row of a batch; <see cref="Index"/> is the row's position in the input.</summary>
public sealed record BatchRowResult(int Index, UpsertOutcome Outcome);
