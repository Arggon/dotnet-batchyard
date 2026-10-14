# ADR 0018: Access meters tenant usage from ingest.completed

- **Status:** Proposed
- **Date:** 2026-10-14
- **Deciders:** Gonzalo (product owner); drafted by the maker agent

## Context

ADR 0017 published `ingest.completed` after every committed batch, with no consumer. An event nobody reads proves nothing about the messaging design, and plans (ADR 0006) are the natural place for usage: a plan's batch limit is already enforced, and usage per tenant is what a quota or a usage-based bill would read.

## Decision

Access subscribes a new queue, `access.usage`, to `ingest.completed` and keeps `dbo.TenantUsage`: one row per tenant per UTC day with batches, items created, updated and rejected. `GET /v1/tenants/me/usage` (permission `items:read`) returns the last 30 days.

- Counting is not idempotent by nature, so the consumer uses the same inbox pattern as the payments read model (ADR 0012): the inbox insert and the counter update share one transaction; a redelivered message changes nothing.
- Access keeps its own copy of the event contract (`IngestCompleted`) instead of referencing `Ingest.Data`, so the two services still deploy independently.
- The day comes from the event's `occurredAt` (UTC), not from when it was consumed, so a delayed message still lands on the right day.

## Consequences

- Usage is eventually consistent: it lags the batch by the outbox poll interval plus broker delivery (well under a second in the smoke test).
- Usage is metering only for now. Enforcing a daily quota would be a follow-up that reads this table; it is not in scope.
- Single-item writes do not publish (ADR 0017), so they are not metered.
