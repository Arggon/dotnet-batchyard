# ADR 0016: OpenTelemetry to the Aspire dashboard

- **Status:** Proposed
- **Date:** 2026-10-14
- **Deciders:** Gonzalo (product owner); drafted by the maker agent

## Context

A request now crosses a gateway, up to three services, SQL Server and RabbitMQ. Logs alone don't show where time goes.

## Decision

`BuildingBlocks.Observability` adds OpenTelemetry tracing (ASP.NET Core, HttpClient, SqlClient, and a `Batchyard.Messaging` source) and metrics (ASP.NET Core, runtime). Export is OTLP and switched on by the standard `OTEL_EXPORTER_OTLP_ENDPOINT` variable; compose runs the standalone Aspire dashboard. Trace context crosses RabbitMQ in a `traceparent` message header. Logs stay on `Microsoft.Extensions.Logging` and go out over OTLP too, instead of adding Serilog.

## Consequences

- No exporter configured means no export, so tests and `dotnet run` stay quiet.
- The dashboard keeps data in memory only; it is a dev tool, not a backend.
- CI reads the dashboard's telemetry HTTP API (`/api/telemetry/*`, Aspire 13.2+, image pinned to 13.6.1) after the smoke test, so a broken exporter or a lost `traceparent` fails the build (`scripts/check-traces.py`). The API is open because compose runs the dashboard with anonymous access; it binds to 127.0.0.1 only.

## Alternatives considered

- **Serilog** (named in the spec): good, but OTLP logs already correlate with traces in the dashboard, and one less dependency keeps the building block small.
- **Jaeger/Grafana stack:** heavier to run locally.
