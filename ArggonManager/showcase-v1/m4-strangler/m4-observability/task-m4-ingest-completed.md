---
type: task
status: done
id: task-m4-ingest-completed
title: "ingest.completed event"
parent: m4-observability
labels: []
created: "2026-10-14"
updated: "2026-10-14"
---
# ingest.completed event

## Context

Ingest writes ingest.completed to its outbox in the same transaction as the batch upsert (ADR 0017). Plan step T5.

## Acceptance

- [ ] Outbox row written with the batch; nothing written when the batch rolls back
- [ ] Event is picked up by the shared outbox dispatcher in an integration test (the RabbitMQ leg of that dispatcher is covered by the payments and access tests)

## Notes
