---
type: task
status: done
id: task-m2-redis-cache
title: Redis permission cache
assignee: Arggon
parent: m2-access-service
labels: []
created: "2026-10-14"
updated: "2026-10-14"
---
# Redis permission cache

## Context

Permission lookups are read on every protected request; cache them in Redis, cache-aside, invalidated on plan change. Plan step T3.

## Acceptance

- [x] Cache-aside via `IDistributedCache` (Redis) with a TTL
- [x] Plan change invalidates the tenant's entries; test proves a stale entry is not served
- [x] Redis in docker compose and Testcontainers

## Notes

### 2026-10-14 @Arggon
Done-gate evidence: shipped in PR #13, landed on main via #26 (d7e18fa). main CI green at 43d80b5: ci https://github.com/Arggon/dotnet-batchyard/actions/runs/38561095595 , codeql .../runs/38561095612 , arggon .../runs/38561095609. Acceptance boxes ticked after checking each against main.
