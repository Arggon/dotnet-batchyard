# ADR 0001: Batch upserts via table-valued parameter and MERGE

- **Status:** Proposed
- **Date:** 2026-10-14
- **Deciders:** Gonzalo (product owner); drafted by the maker agent

## Context

Tenants upload thousands of items at a time. The batch path has to commit valid rows, reject invalid ones with a reason, and tell the client per row whether it was created or updated. It must also stay readable for a reviewer.

## Decision

Validate in memory, send the valid rows as a table-valued parameter (`dbo.ItemBatchRow`) to `usp_UpsertItemsBatch`, and upsert them with one set-based `MERGE` inside one transaction. The procedure returns one outcome row per input row (`OUTPUT $action`), so the API can answer per row without a second query.

## Consequences

- One round trip and one transaction per batch; the procedure is versioned SQL in `db/ingest/`.
- `MERGE` needs unique source keys, so duplicates are removed before the statement (first wins) and reported as rejected.
- TVPs are SQL Server specific; that is accepted, the CV claim is about SQL Server.

## Alternatives considered

- **SqlBulkCopy into a staging table, then MERGE:** fastest for very large loads (100k+), but two steps, a staging table per session and no natural per-row outcome. Worth revisiting if batches grow far past 5,000.
- **Per-row INSERT/UPDATE in a loop:** the "before" this repo measures against.
- **EF Core `ExecuteUpdate`/bulk extensions:** hides the SQL the CV claim is about; popular bulk extensions are commercially licensed.
