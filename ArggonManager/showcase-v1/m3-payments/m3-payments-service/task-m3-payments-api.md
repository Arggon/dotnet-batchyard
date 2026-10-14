---
type: task
status: done
id: task-m3-payments-api
title: "Payments data, idempotent capture and API"
parent: m3-payments-service
labels: []
created: "2026-10-14"
updated: "2026-10-14"
---
# Payments data, idempotent capture and API

## Context

SQL schema and Dapper repositories, unique index on idempotency keys, optimistic concurrency, fake gateway with retries and timeout, HTTP endpoints. Plan step T2.

## Acceptance

- [x] Integration test: capture twice with one key -> one charge, one payment row
- [x] Concurrent captures with different keys -> one wins, the other gets 409
- [x] Gateway retries transient failures and times out (Polly)
- [x] Endpoints protected by payments:read / payments:write

## Notes

### 2026-10-14 @Arggon
Done-gate evidence: shipped in PR #17, landed on main via #26 (d7e18fa). main CI green at 43d80b5: ci https://github.com/Arggon/dotnet-batchyard/actions/runs/38561095595 , codeql .../runs/38561095612 , arggon .../runs/38561095609. Acceptance boxes ticked after checking each against main.
