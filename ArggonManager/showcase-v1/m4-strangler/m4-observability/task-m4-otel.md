---
type: task
status: done
id: task-m4-otel
title: "OpenTelemetry tracing"
parent: m4-observability
labels: []
created: "2026-10-14"
updated: "2026-10-14"
---
# OpenTelemetry tracing

## Context

BuildingBlocks.Observability: traces (ASP.NET Core, HttpClient, SqlClient, messaging) and metrics over OTLP; Aspire dashboard in compose; traceparent carried in RabbitMQ headers (ADR 0016). Plan step T4.

## Acceptance

- [ ] Every service and the gateway export traces and metrics when an OTLP endpoint is set
- [ ] Test: publish and consume share one trace id
- [ ] Aspire dashboard in compose

## Notes
