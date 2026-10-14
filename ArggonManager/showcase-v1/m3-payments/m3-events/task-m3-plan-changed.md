---
type: task
status: done
id: task-m3-plan-changed
title: plan.changed drives the permission cache
parent: m3-events
labels: []
created: "2026-10-14"
updated: "2026-10-14"
---
# plan.changed drives the permission cache

## Context

Access publishes plan.changed through its own outbox and clears the permission cache when the event arrives (ADR 0013). Plan step T4.

## Acceptance

- [ ] Plan change writes plan.changed in the same transaction
- [ ] Access consumer bumps the tenant cache generation on plan.changed
- [ ] Test: plan change -> event -> cache cleared, against real RabbitMQ and Redis

## Notes
