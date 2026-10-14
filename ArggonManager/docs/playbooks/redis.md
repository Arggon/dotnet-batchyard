---
playbook_id: redis
version: 7.4
researched: 2026-10-14
status: current
---

# Redis playbook (redis)

Technology playbook: the chosen version and the current best practices for
redis. The version/best-practices research happened when this file was
created — cite dated sources (URL + access date) in every section so the next
reader can re-verify, and keep this file current via
`arggon playbook status`.

## Setup

- `redis:7.4-alpine` in compose and Testcontainers (`Testcontainers.Redis`). Redis 7.4 is the last release under the BSD license before the 2024 license change; a managed service (ElastiCache/Valkey) would replace it in production. Source: https://redis.io/blog/redis-adopts-dual-source-available-licensing/ (accessed 2026-10-14).
- Client: `Microsoft.Extensions.Caching.StackExchangeRedis` (`IDistributedCache`).

## Conventions

- Keys are `service:purpose:{ids}`; every entry has a TTL.
- Cache-aside only: the database stays the source of truth.

## Testing

- Real Redis via Testcontainers; tests assert cache hits by changing the database behind the cache.

## Security

- No secrets cached. Redis is not exposed outside the compose network in production setups.

## Upgrade policy

- Re-evaluate Valkey vs Redis at the next refresh; refresh with `arggon playbook refresh redis --version <v>`.
