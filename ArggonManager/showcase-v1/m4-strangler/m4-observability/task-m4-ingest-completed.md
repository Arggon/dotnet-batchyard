---
type: task
status: done
id: task-m4-ingest-completed
title: ingest.completed event
parent: m4-observability
labels: []
created: "2026-10-14"
updated: "2026-10-14"
---
# ingest.completed event

## Context

Ingest writes ingest.completed to its outbox in the same transaction as the batch upsert (ADR 0017). Plan step T5.

## Acceptance

- [x] Outbox row written with the batch; nothing written when the batch rolls back
- [x] Event is picked up by the shared outbox dispatcher in an integration test (the RabbitMQ leg of that dispatcher is covered by the payments and access tests)

## Notes

### 2026-10-14 @Arggon
Done-gate evidence: shipped in PR #25, landed on main via #26 (d7e18fa). main CI green at 43d80b5: ci https://github.com/Arggon/dotnet-batchyard/actions/runs/38561095595 , codeql .../runs/38561095612 , arggon .../runs/38561095609. Acceptance boxes ticked after checking each against main.
