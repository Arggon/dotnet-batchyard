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

### 2026-10-14 @Arggon
Ready for acceptance (Gonzalo): all child tasks are done with evidence (merged PRs, main CI green at 43d80b5 https://github.com/Arggon/dotnet-batchyard/actions/runs/38561095595). The container stays open on purpose: product acceptance is a human decision. To accept, add a comment 'accept: approve' (or 'accept: changes-requested'), then close it. Deliverables: #18 #19.
