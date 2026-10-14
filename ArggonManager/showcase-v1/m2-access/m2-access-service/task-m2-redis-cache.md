---
type: task
status: todo
id: task-m2-redis-cache
title: Redis permission cache
parent: m2-access-service
labels: []
created: "2026-10-14"
updated: "2026-10-14"
---
# Redis permission cache

## Context

Permission lookups are read on every protected request; cache them in Redis, cache-aside, invalidated on plan change. Plan step T3.

## Acceptance

- [ ] Cache-aside via `IDistributedCache` (Redis) with a TTL
- [ ] Plan change invalidates the tenant's entries; test proves a stale entry is not served
- [ ] Redis in docker compose and Testcontainers

## Notes
