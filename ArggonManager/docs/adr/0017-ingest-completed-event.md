# ADR 0017: ingest.completed through the outbox

- **Status:** Proposed
- **Date:** 2026-10-14
- **Deciders:** Gonzalo (product owner); drafted by the maker agent

## Context

Other parts of the system (search indexing, notifications) will want to know when a tenant's price list landed.

## Decision

After a batch commits, ingest publishes `ingest.completed` with counts only (tenant, created, updated, rejected). The row is written to ingest's outbox inside the same transaction as the upsert, using the shared dispatcher from ADR 0011. No consumer ships in M4; ADR 0018 adds one (the Access usage meter).

## Consequences

- The batch path gets one extra INSERT per request, not per row.
- Single-item writes do not publish (too chatty); only batches do.
