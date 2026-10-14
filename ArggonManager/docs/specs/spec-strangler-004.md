---
spec_id: strangler-004
title: "Strangler: legacy monolith, gateway, telemetry"
status: proposed
created: 2026-10-14
---

# Spec: Strangler: legacy monolith, gateway, telemetry (strangler-004)

## Purpose

Show how the ingest service was pulled out of a monolith without a big-bang switch, measure what that bought, and make one request traceable across every hop. This is the generic version of "a .NET microservice extracted from a monolith" on the CV.

Invariants:

- **Same contract.** Clients cannot tell which backend served `/v1/items`, except for the `X-Backend` diagnostic header.
- **One switch per route.** Moving a route is a config change, not a code change, and can be undone the same way.
- **Shared data during migration.** The monolith and ingest-svc write the same `dbo.Items` table, so either can serve a route at any time.
- **Measured, not claimed.** The k6 comparison reports whatever it measures, with the environment.

## Synopsis

```http
POST /v1/items:batch      (gateway :8000 → legacy or ingest, by config)
GET  /v1/items/{sku}      (gateway → legacy or ingest)
GET  /v1/reports/stock    (gateway → legacy only: not migrated yet)
/v1/auth/*, /v1/tenants/* → access; /v1/invoices/*, /v1/payments* → payments
```

## Behavior

- Legacy batch: validates each row, then for each row runs a SELECT and an INSERT or UPDATE, all in one transaction. Same per-row results as ingest.
- Gateway: YARP, routes and clusters in configuration. `ReverseProxy:Routes:<route>:ClusterId` decides the backend.
- Telemetry: W3C trace context over HTTP and RabbitMQ; OTLP export to the Aspire dashboard in compose.
- `ingest.completed {tenantId, created, updated, rejected, occurredAt}` after each committed batch.

## Out of scope

Data migration between separate databases, canary percentages, and auth at the gateway (each service still validates the JWT).
