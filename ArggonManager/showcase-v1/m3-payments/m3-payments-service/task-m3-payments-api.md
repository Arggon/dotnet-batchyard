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

- [ ] Integration test: capture twice with one key -> one charge, one payment row
- [ ] Concurrent captures with different keys -> one wins, the other gets 409
- [ ] Gateway retries transient failures and times out (Polly)
- [ ] Endpoints protected by payments:read / payments:write

## Notes
