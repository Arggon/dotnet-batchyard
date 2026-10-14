# ADR 0002: Dapper on hot paths, EF Core for simple CRUD

- **Status:** Proposed
- **Date:** 2026-10-14
- **Deciders:** Gonzalo (product owner); drafted by the maker agent

## Context

Ingest and payment reads are hot paths where the exact SQL matters. Access (plans, roles, permissions) is plain CRUD with relations.

## Decision

Use Dapper plus versioned SQL (DbUp) for Ingest and for Payments reads. Use EF Core for Access (M2). Schema changes for all services are plain SQL scripts run by DbUp, so stored procedures are reviewed like any other code.

## Consequences

- Two data-access styles in one repo, each where it fits; the choice is visible to a reviewer.
- EF Core migrations are not used, to keep a single migration mechanism.

## Alternatives considered

- **EF Core everywhere:** less code for CRUD, but TVP + proc calls end up as raw SQL anyway.
- **Dapper everywhere:** more hand-written mapping in Access for little benefit.
