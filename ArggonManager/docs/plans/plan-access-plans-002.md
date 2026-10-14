---
plan_id: access-plans-002
title: "Plan for Access: plans, roles and permissions"
spec: ArggonManager/docs/specs/spec-access-plans-002.md
status: proposed
created: 2026-10-14
---

# Plan: Access: plans, roles and permissions (access-plans-002)

Derived from `ArggonManager/docs/specs/spec-access-plans-002.md`. One PR per task, stacked in order.

## Tasks

### T1: Data model and plan changes (task-m2-access-data)

- `db/access/` schema + seeded catalog, EF Core context, permission resolver, plan change service, secret hashing.
- **Acceptance:** Testcontainers tests for resolution and plan change.

### T2: Access API and JWT (task-m2-access-api)

- Token endpoint with rate limiting, JWT issuing/validation shared via BuildingBlocks, permission endpoint filter, webhook stub, access matrix tests.
- **Acceptance:** matrix theories green; SECURITY.md updated. Depends on T1.

### T3: Redis permission cache (task-m2-redis-cache)

- Cache-aside over the resolver, invalidated on plan change; Redis in compose.
- **Acceptance:** tests against real Redis prove hits and invalidation. Depends on T2.

### T4: Ingest secured by JWT (task-m2-ingest-jwt)

- Replace `X-Tenant-Id` with the token; permission filters; batch limit claim; tools and docs fetch tokens.
- **Acceptance:** ingest matrix theories green; compose smoke and k6 green in CI. Depends on T2.
