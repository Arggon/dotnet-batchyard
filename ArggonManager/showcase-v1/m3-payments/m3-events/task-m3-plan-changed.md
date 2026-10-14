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

- [x] Plan change writes plan.changed in the same transaction
- [x] Access consumer bumps the tenant cache generation on plan.changed
- [x] Test: plan change -> event -> cache cleared, against real RabbitMQ and Redis

## Notes

### 2026-10-14 @Arggon
Done-gate evidence: shipped in PR #19, landed on main via #26 (d7e18fa). main CI green at 43d80b5: ci https://github.com/Arggon/dotnet-batchyard/actions/runs/38561095595 , codeql .../runs/38561095612 , arggon .../runs/38561095609. Acceptance boxes ticked after checking each against main.
