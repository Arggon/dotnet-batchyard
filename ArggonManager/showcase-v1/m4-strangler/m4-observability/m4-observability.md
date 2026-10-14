---
type: story
status: todo
id: m4-observability
title: Tracing and ingest events
parent: m4-strangler
labels: []
created: "2026-10-14"
updated: "2026-10-14"
---
# Tracing and ingest events

## Context

One trace follows a request through the gateway, a service, SQL Server and RabbitMQ. Ingest announces finished batches with ingest.completed.

## Acceptance

- [ ] OpenTelemetry traces and metrics from every service to the Aspire dashboard
- [ ] Trace context crosses RabbitMQ
- [ ] ingest.completed published through the outbox

## Notes

### 2026-10-14 @Arggon
Ready for acceptance (Gonzalo): all child tasks are done with evidence (merged PRs, main CI green at 43d80b5 https://github.com/Arggon/dotnet-batchyard/actions/runs/38561095595). The container stays open on purpose: product acceptance is a human decision. To accept, add a comment 'accept: approve' (or 'accept: changes-requested'), then close it. Deliverables: #24 #25. Still open, not delivered: (1) nothing consumes ingest.completed yet; it is written to the outbox and picked up by the dispatcher. (2) The benchmarks were not re-measured after ingest.completed added an outbox write per batch. (3) The Aspire dashboard view is not asserted in CI; only the trace-id test is.
