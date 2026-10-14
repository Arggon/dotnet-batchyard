---
type: task
status: done
id: task-m4-otel
title: OpenTelemetry tracing
parent: m4-observability
labels: []
created: "2026-10-14"
updated: "2026-10-14"
---
# OpenTelemetry tracing

## Context

BuildingBlocks.Observability: traces (ASP.NET Core, HttpClient, SqlClient, messaging) and metrics over OTLP; Aspire dashboard in compose; traceparent carried in RabbitMQ headers (ADR 0016). Plan step T4.

## Acceptance

- [x] Every service and the gateway export traces and metrics when an OTLP endpoint is set
- [x] Test: publish and consume share one trace id
- [x] Aspire dashboard in compose

## Notes

### 2026-10-14 @Arggon
Done-gate evidence: shipped in PR #24, landed on main via #26 (d7e18fa). main CI green at 43d80b5: ci https://github.com/Arggon/dotnet-batchyard/actions/runs/38561095595 , codeql .../runs/38561095612 , arggon .../runs/38561095609. Acceptance boxes ticked after checking each against main.
