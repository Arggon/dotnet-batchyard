# ADR 0008: Cache permission lookups in Redis (cache-aside)

- **Status:** Proposed
- **Date:** 2026-10-14
- **Deciders:** Gonzalo (product owner); drafted by the maker agent

## Context

Access resolves a client's effective permissions on every protected request and on every token issue. That is a 4-table join that rarely changes.

## Decision

Cache-aside through `IDistributedCache` backed by Redis: key `access:perm:{tenant}:{generation}:{client}`, 10-minute TTL. A plan change writes a new per-tenant generation right after the change commits, so all of the tenant's old entries stop being read at once and expire on their TTL (no key scans, works with plain `IDistributedCache`). When RabbitMQ arrives in M3, the same invalidation hangs off the `plan.changed` event.

## Consequences

- Redis is a new runtime dependency (compose, Testcontainers).
- If Redis is down, lookups fall back to SQL and log a warning instead of failing requests.
- There is a small window between commit and invalidation; the TTL bounds it.

## Alternatives considered

- **In-memory cache:** wrong once there are two instances.
- **No cache:** fine at demo scale, but the brief asks for Redis on this path.
