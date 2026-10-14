---
type: initiative
status: todo
id: showcase-v1
title: Showcase v1
labels: []
created: "2026-10-14"
updated: "2026-10-14"
---
# Showcase v1

## Context

Gonzalo's public GitHub has no C#, and he is applying as a senior .NET backend engineer. This initiative builds a small, readable monorepo that proves the CV claims (batch ingest, monolith extraction, access/monetization roles, CQRS payments) with code, tests and measured numbers. Source brief: the showcase repo spec (kept outside this repo).

## Acceptance

- [ ] M1: skeleton, CI, compose, ingest single + batch endpoints with the stored procedure, integration tests, first benchmark
- [ ] M2: access-svc (plans, roles, permissions, JWT, authorization matrix, Redis cache)
- [ ] M3: payments-svc (CQRS + hand-rolled mediator, idempotency, outbox + RabbitMQ, read model)
- [ ] M4: legacy monolith + YARP strangler + k6 comparison, OpenTelemetry, README polish

## Notes

### 2026-10-14 @Arggon
Status 2026-10-14: all 21 tasks for M1–M4 are done; merged PRs #1–#26 plus fixes #27 and #28; main CI green at 43d80b5. Stories, epics and the initiative stay open until Gonzalo accepts them. Also still open: ADRs 0001–0017 and specs 001–004 are still Proposed/proposed. Accepting them is Gonzalo's decision.
