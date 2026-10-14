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

- [ ] Events are written in the same transaction as the state change
- [ ] Dispatcher publishes with confirms and marks rows published; retries on failure
- [ ] Read model consumer is idempotent (inbox table); ListTenantPayments reads it
- [ ] Tests against real RabbitMQ

## Notes
