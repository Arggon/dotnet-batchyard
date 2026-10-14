---
type: task
status: done
id: task-m3-outbox
title: "Outbox, RabbitMQ and payments read model"
parent: m3-events
labels: []
created: "2026-10-14"
updated: "2026-10-14"
---
# Outbox, RabbitMQ and payments read model

## Context

Outbox table + dispatcher to RabbitMQ (ADR 0011) and the read model fed by payment events (ADR 0012). Plan step T3.

## Acceptance

- [x] Events are written in the same transaction as the state change
- [x] Dispatcher publishes with confirms and marks rows published; retries on failure
- [x] Read model consumer is idempotent (inbox table); ListTenantPayments reads it
- [x] Tests against real RabbitMQ

## Notes

### 2026-10-14 @Arggon
Done-gate evidence: shipped in PR #18, landed on main via #26 (d7e18fa). main CI green at 43d80b5: ci https://github.com/Arggon/dotnet-batchyard/actions/runs/38561095595 , codeql .../runs/38561095612 , arggon .../runs/38561095609. Acceptance boxes ticked after checking each against main.
