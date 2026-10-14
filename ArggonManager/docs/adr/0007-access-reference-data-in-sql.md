# ADR 0007: Plans, roles and permissions are versioned reference data

- **Status:** Proposed
- **Date:** 2026-10-14
- **Deciders:** Gonzalo (product owner); drafted by the maker agent

## Context

The catalog of plans, roles and permissions decides what customers can do. It must be reviewable and identical in every environment.

## Decision

The catalog is seeded by DbUp scripts in `db/access/`, like the schema. EF Core (ADR 0002) maps the tables for reads and writes but does not own migrations. Changing what a plan grants is a new SQL script in a PR.

## Consequences

- Authorization changes get code review and history.
- No admin UI for editing the catalog in v1.

## Alternatives considered

- **EF Core migrations + `HasData`:** a second migration tool next to DbUp.
- **Catalog in appsettings:** not queryable, drifts between environments.
