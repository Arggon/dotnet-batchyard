---
type: task
status: done
id: task-m4-usage-meter
title: "Consume ingest.completed: Access usage meter"
assignee: Arggon
parent: m4-observability
labels: []
created: "2026-10-14"
updated: "2026-10-14"
---
<!--
  Placement (v0): ArggonManager/showcase-v1/m4-strangler/m4-observability/task-m4-usage-meter.md
  Leaves live only under a story. id is the filename stem: task-m4-usage-meter.
  CLI `arggon create task m4-usage-meter` adds the task- prefix (do not pass it twice).
  parent MUST be the story id. Omit assignee when unassigned. Omit blocked_reason unless status is blocked.
-->

# Consume ingest.completed: Access usage meter

## Context

Open note on m4-observability: ingest.completed was published with no consumer. Gonzalo asked for a real, tested, documented consumer before acceptance (ADR 0018).

## Acceptance

- [x] Access consumes ingest.completed on its own queue (access.usage) into dbo.TenantUsage (per tenant, per UTC day)
- [x] Redelivery is counted once (inbox in the same transaction), tested
- [x] End-to-end test over real RabbitMQ: published event -> GET /v1/tenants/me/usage; endpoint in the authorization matrix
- [x] Compose smoke test proves batch -> outbox -> RabbitMQ -> usage meter
- [x] ADR 0018, README and architecture diagram updated

## Notes

### 2026-10-14 @Arggon
Evidence (this PR): src/Access.Data/UsageMeter.cs, db/access/0006, tests/Access.ApiTests/UsageMeterTests.cs (4 tests, real RabbitMQ + SQL Server), matrix row added; scripts/smoke.sh checks the usage meter after a real batch; ADR 0018. Full local suite green (159 tests). Done-gate CI link: see the PR checks; final main run to be linked after merge.
