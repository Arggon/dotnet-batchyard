# ADR 0011: Transactional outbox to RabbitMQ

- **Status:** Proposed
- **Date:** 2026-10-14
- **Deciders:** Gonzalo (product owner); drafted by the maker agent

## Context

Services must publish events (`payment.captured`, `plan.changed`) without losing them when RabbitMQ is down and without publishing events for changes that rolled back.

## Decision

Each service writes events to its own `dbo.Outbox` table in the same transaction as the state change. A background dispatcher reads unpublished rows, publishes them to the `batchyard.events` topic exchange with publisher confirms, and marks them published. Delivery is at least once; consumers record handled message ids in `dbo.Inbox` to drop duplicates. A thin wrapper over `RabbitMQ.Client` (no MassTransit/NServiceBus).

## Consequences

- No lost events and no phantom events.
- Events arrive a little later (poll interval, default 1 s).
- Order is per outbox, not global; consumers must not depend on cross-service order.

## Alternatives considered

- **Publish right after commit:** loses events if the process dies in between.
- **MassTransit:** has an outbox built in, but its newer versions are commercially licensed and it hides the mechanism.
