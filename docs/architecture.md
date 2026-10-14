# Architecture

Two views: the system as it is today (milestone 1), and the full target from the roadmap. Solid boxes exist in the code; dashed boxes are planned.

## Today (M1)

```mermaid
flowchart LR
    client([API client / demo client / k6])
    subgraph compose [docker compose]
        api["Ingest.Api<br/>ASP.NET Core Minimal API<br/>.NET 10"]
        db[("SQL Server 2022<br/>Items, IdempotencyKeys<br/>usp_UpsertItem, usp_UpsertItemsBatch")]
    end
    client -->|"POST /v1/items<br/>POST /v1/items:batch<br/>X-Tenant-Id, Idempotency-Key"| api
    api -->|"Dapper: stored procedures,<br/>one TVP per batch"| db
    api -.->|"DbUp migrations on startup<br/>(db/ingest/*.sql)"| db
```

### Inside the ingest service

```mermaid
flowchart TB
    subgraph api [Ingest.Api]
        endpoints["ItemEndpoints<br/>routes, tenant header, Problem Details"]
        validator["ItemValidator<br/>in-memory rules"]
        idem["Idempotency<br/>hash body, replay or 409"]
    end
    subgraph data [Ingest.Data]
        repo["SqlItemRepository<br/>Dapper"]
        store["SqlIdempotencyStore"]
        migrator["DatabaseMigrator<br/>DbUp"]
    end
    bb["BuildingBlocks<br/>TenantHeader"]
    endpoints --> validator
    endpoints --> idem
    endpoints --> repo
    idem --> store
    endpoints --> bb
```

`Ingest.Data` must not depend on `Ingest.Api` or ASP.NET Core. `tests/Architecture.Tests` enforces this rule.

### One batch request

```mermaid
sequenceDiagram
    participant C as Client
    participant A as Ingest.Api
    participant S as SQL Server
    C->>A: POST /v1/items:batch (N rows)
    A->>A: validate each row in memory
    Note over A: invalid rows → rejected + reason
    A->>S: EXEC usp_UpsertItemsBatch @Rows (one TVP)
    S->>S: rank by SKU (first wins), one MERGE, one transaction
    S-->>A: (RowIndex, created|updated|duplicate) per row
    A-->>C: 200 {created, updated, rejected, results[] in input order}
```

## Target (M4)

```mermaid
flowchart LR
    client([Clients])
    gw["Gateway (YARP)<br/>route-level strangler switch"]
    ingest[Ingest.Api]
    access["Access.Api<br/>plans → roles → permissions"]:::planned
    payments["Payments.Api<br/>CQRS + mediator"]:::planned
    legacy["Legacy.Monolith<br/>the before"]:::planned
    sql[(SQL Server)]
    redis[(Redis)]:::planned
    mq{{RabbitMQ<br/>ingest.completed, payment.captured, plan.changed}}:::planned
    otel["OpenTelemetry → Aspire dashboard"]:::planned

    client --> gw
    gw --> ingest
    gw -.-> access
    gw -.-> payments
    gw -.-> legacy
    ingest --> sql
    access -.-> sql
    access -.-> redis
    payments -.-> sql
    ingest -.->|outbox| mq
    payments -.->|outbox| mq
    mq -.-> access

    classDef planned stroke-dasharray: 5 5
```

| Milestone | Adds |
|---|---|
| M1 ✅ | Ingest service, SQL Server, compose, CI |
| M2 | Access service, JWT, Redis permission cache |
| M3 | Payments service, mediator, outbox + RabbitMQ, read model |
| M4 | Legacy monolith, YARP gateway, OpenTelemetry |

Decisions behind these choices are in [ArggonManager/docs/adr](../ArggonManager/docs/adr/).
