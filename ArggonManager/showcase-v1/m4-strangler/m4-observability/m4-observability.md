---
type: story
status: todo
id: m4-observability
title: "Tracing and ingest events"
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
