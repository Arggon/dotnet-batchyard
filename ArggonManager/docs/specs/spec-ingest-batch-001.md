---
spec_id: ingest-batch-001
title: Ingest batch endpoint
status: proposed
created: 2026-10-14
---

# Spec: Ingest batch endpoint (ingest-batch-001)

## Purpose

Tenants of the parts marketplace upload large product and price lists. Sending
one HTTP request and one database round trip per item is slow and chatty. This
spec adds a batch endpoint that validates in memory, ships the valid rows to
SQL Server as a table-valued parameter, and upserts them with one set-based
`MERGE` inside one transaction.

Invariants:

- **Partial failure, never partial corruption.** Invalid rows are rejected with
  a reason; valid rows commit. A database error rolls back the whole batch.
- **One round trip per batch** for the data path (no per-row queries).
- **Parameterized SQL only.** No SQL text is built from input.
- **Results are per row and in input order**, each tagged `created`,
  `updated` or `rejected` (+ reason).
- **Replays are safe.** The same `Idempotency-Key` with the same body returns
  the stored response; with a different body it is refused.

## Synopsis

```http
POST /v1/items            X-Tenant-Id: <guid>   Idempotency-Key: <optional>
{ "sku": "BRK-001", "name": "Brake pad", "price": 12.50, "currency": "USD", "quantity": 40 }
-> 201 { "sku": "BRK-001", "outcome": "created" }   (200 + "updated" when it existed)

POST /v1/items:batch      X-Tenant-Id: <guid>   Idempotency-Key: <optional>
{ "items": [ { ...item... }, ... ] }                (1..MaxBatchSize, default 5000)
-> 200 { "created": 2, "updated": 1, "rejected": 1,
         "results": [ { "index": 0, "sku": "A", "outcome": "created" },
                      { "index": 3, "sku": "",  "outcome": "rejected", "reason": "sku is required" } ] }
```

Errors use Problem Details (RFC 9457): missing/invalid tenant, empty batch or
batch over the limit -> 400; idempotency key reused with a different body -> 409.

Validation rules: `sku` 1-64 chars of `[A-Za-z0-9._-]`; `name` 1-200 chars;
`price` >= 0 with at most 4 decimals; `currency` a 3-letter upper-case ISO code;
`quantity` >= 0. A SKU repeated inside one batch: the first occurrence wins,
later ones are rejected with `duplicate sku in batch`.

## Acceptance

- [ ] `POST /v1/items` creates (201) and updates (200) an item; invalid input -> 400 Problem Details
- [ ] `POST /v1/items:batch` returns per-row outcomes in input order; valid rows commit when other rows are invalid
- [ ] Batch path is one call to `usp_UpsertItemsBatch` with a TVP; the proc runs one `MERGE` in one transaction
- [ ] Duplicate SKUs inside one batch are rejected (first wins), never a MERGE error
- [ ] Batch over `Ingest:MaxBatchSize` (default 5000, configurable) or empty -> 400
- [ ] `Idempotency-Key`: same key + same body replays the stored response; same key + different body -> 409
- [ ] Integration tests run against real SQL Server via Testcontainers (no database mocks)
- [ ] BenchmarkDotNet compares one-by-one vs batch; measured numbers in `docs/benchmarks.md`
