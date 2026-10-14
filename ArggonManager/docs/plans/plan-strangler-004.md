---
plan_id: strangler-004
title: "Plan for Strangler: legacy monolith, gateway, telemetry"
spec: ArggonManager/docs/specs/spec-strangler-004.md
status: proposed
created: 2026-10-14
---

# Plan: Strangler: legacy monolith, gateway, telemetry (strangler-004)

Derived from `ArggonManager/docs/specs/spec-strangler-004.md`. One PR per task, stacked in order.

## Tasks

### T1: Legacy monolith (task-m4-legacy)

- `Legacy.Monolith`, per-row queries over the shared items table, plus a stock report route that stays legacy.
- **Acceptance:** integration tests against SQL Server; same contract as ingest.

### T2: YARP gateway (task-m4-gateway)

- `Gateway` with config-driven routes and an `X-Backend` header; compose and smoke test go through it.
- **Acceptance:** switching tests with fake backends. Depends on T1.

### T3: k6 comparison (task-m4-k6-compare)

- `benchmarks/k6/strangler.js`, measured results in `docs/benchmarks.md`. Depends on T2.

### T4: OpenTelemetry (task-m4-otel)

- `BuildingBlocks.Observability`, Aspire dashboard, trace context over RabbitMQ.
- **Acceptance:** test that publish and consume share a trace. Depends on T2.

### T5: ingest.completed (task-m4-ingest-completed)

- Ingest outbox in the batch transaction, dispatcher in Ingest.Api.
- **Acceptance:** rollback writes no event; the shared dispatcher picks the event up (its RabbitMQ leg is tested in payments and access). Depends on T4.
