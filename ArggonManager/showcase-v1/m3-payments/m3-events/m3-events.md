---
type: story
status: todo
id: m3-events
title: Events over RabbitMQ
parent: m3-payments
labels: []
created: "2026-10-14"
updated: "2026-10-14"
---
# Events over RabbitMQ

## Context

Services tell each other what happened through RabbitMQ, written via a transactional outbox so no event is lost or invented.

## Acceptance

- [ ] payment.captured / payment.refunded feed the payments read model
- [ ] plan.changed clears the Access permission cache

## Notes
