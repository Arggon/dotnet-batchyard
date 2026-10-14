# ADR 0015: Extracting ingest from the monolith

- **Status:** Proposed
- **Date:** 2026-10-14
- **Deciders:** Gonzalo (product owner); drafted by the maker agent

## Context

The legacy app writes items with one SELECT plus one INSERT or UPDATE per row. Large price lists are slow and hold a transaction open for the whole loop. Items are the hottest path and have a clear boundary (tenant + SKU), so they are the first thing to extract.

## Options

1. Optimize inside the monolith.
2. Big-bang rewrite and cut over.
3. Strangler: build ingest-svc with the same contract, route traffic to it through a gateway one route at a time, keep the legacy route as a fallback.

## Decision

Option 3. Both apps share the `dbo.Items` table during the migration, so a route can move back at any time. The stock report stays in the monolith for now to show a partly migrated system.

## Consequences

- Two writers to one table until the legacy routes are deleted; the schema is owned by ingest-svc's DbUp scripts and the monolith must not change it.
- The comparison is measured with k6 through the gateway (docs/benchmarks.md).
- Next step after M4: remove the legacy item routes, then the shared table coupling.
