---
plan_id: payments-003
title: "Plan for Payments: CQRS, mediator and events"
spec: ArggonManager/docs/specs/spec-payments-003.md
status: proposed
created: 2026-10-14
---

# Plan: Payments: CQRS, mediator and events (payments-003)

Derived from `ArggonManager/docs/specs/spec-payments-003.md`. One PR per task, stacked in order.

## Tasks

### T1: Mediator, commands and queries (task-m3-mediator)

- `Payments.Application`: mediator, behaviors, domain, handlers over repository interfaces.
- **Acceptance:** unit tests for the mediator pipeline and every handler.

### T2: Data, idempotent capture and API (task-m3-payments-api)

- `Payments.Data` (Dapper + DbUp), fake gateway with Polly, `Payments.Api`, payments permissions in the access catalog.
- **Acceptance:** "capture twice -> one charge" and the concurrency test against real SQL Server. Depends on T1.

### T3: Outbox, RabbitMQ and read model (task-m3-outbox)

- `BuildingBlocks.Messaging` (outbox writer, dispatcher, RabbitMQ publisher/consumer, inbox), payments read model.
- **Acceptance:** tests against real RabbitMQ; compose smoke covers a capture. Depends on T2.

### T4: plan.changed drives the permission cache (task-m3-plan-changed)

- Access writes plan.changed to its outbox; its consumer clears the cache.
- **Acceptance:** event-driven invalidation test against real RabbitMQ and Redis. Depends on T3.
