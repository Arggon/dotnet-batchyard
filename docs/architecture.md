# Architecture

Two views: the system as it is today (milestone 3), and the full target from the roadmap. Solid boxes exist in the code; dashed boxes are planned.

## Today (M3)

```mermaid
flowchart LR
    client([API client / demo client / k6])
    market([Marketplace webhook])
    subgraph compose [docker compose]
        access["Access.Api :8081<br/>token, plans, roles, permissions"]
        api["Ingest.Api :8080<br/>items, batches"]
        payments["Payments.Api :8082<br/>commands + queries via mediator"]
        db[("SQL Server 2022<br/>batchyard (items)<br/>batchyard_access (catalog, outbox)<br/>batchyard_payments (write, outbox, read model)")]
        redis[("Redis<br/>permission cache")]
        mq{{"RabbitMQ<br/>exchange batchyard.events"}}
    end
    client -->|"1. POST /v1/auth/token"| access
    client -->|"2. Bearer JWT<br/>POST /v1/items:batch"| api
    client -->|"Bearer JWT<br/>invoices, capture, refund"| payments
    market -->|"subscribe / unsubscribe"| access
    access -->|EF Core| db
    access -->|cache-aside| redis
    api -->|"Dapper: stored procedures,<br/>one TVP per batch"| db
    payments -->|Dapper| db
    payments -->|"outbox: payment.captured / refunded"| mq
    access -->|"outbox: plan.changed"| mq
    mq -->|"queue payments.read-model"| payments
    mq -->|"queue access.cache → bump generation"| access
```

Ingest never calls Access. It validates the token's signature and reads `tenant_id`, `perm` and `max_batch` from it (ADR 0006).

### Inside the ingest service

```mermaid
flowchart TB
    subgraph api [Ingest.Api]
        endpoints["ItemEndpoints<br/>routes, RequirePermission, Problem Details"]
        validator["ItemValidator<br/>in-memory rules"]
        idem["Idempotency<br/>hash body, replay or 409"]
    end
    subgraph data [Ingest.Data]
        repo["SqlItemRepository<br/>Dapper"]
        store["SqlIdempotencyStore"]
        migrator["DatabaseMigrator<br/>DbUp"]
    end
    bb["BuildingBlocks.Auth<br/>JWT validation, tenant + permissions from claims"]
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
    C->>A: POST /v1/items:batch (N rows, Bearer JWT)
    A->>A: check signature, ingest:batch, N ≤ max_batch
    A->>A: validate each row in memory
    Note over A: invalid rows → rejected + reason
    A->>S: EXEC usp_UpsertItemsBatch @Rows (one TVP)
    S->>S: rank by SKU (first wins), one MERGE, one transaction
    S-->>A: (RowIndex, created|updated|duplicate) per row
    A-->>C: 200 {created, updated, rejected, results[] in input order}
```

### Inside the payments service

```mermaid
sequenceDiagram
    participant C as Client
    participant A as Payments.Api
    participant M as Mediator pipeline
    participant S as SQL Server
    participant G as Gateway (fake)
    participant D as Outbox dispatcher
    participant R as RabbitMQ
    C->>A: POST /v1/invoices/{id}/capture (Idempotency-Key)
    A->>M: CapturePayment
    M->>M: log, validate, BEGIN TRAN
    M->>S: read invoice (404 if not this tenant's)
    M->>S: payment for this key? → replay (200)
    M->>S: UPDATE invoice SET paid WHERE Version = @v (claim)
    M->>G: charge (retry + timeout, idempotent by key)
    M->>S: INSERT payment (unique key) + INSERT outbox
    M->>S: COMMIT
    A-->>C: 201 receipt
    D->>S: claim unpublished rows (UPDLOCK, READPAST)
    D->>R: publish, wait for confirm
    D->>S: mark published
    R-->>A: payment.captured → inbox + TenantPayments
```

`Payments.Application` holds the mediator, handlers and domain, and has no reference to SQL, HTTP or RabbitMQ. `tests/Architecture.Tests` enforces that, and also that `BuildingBlocks.Messaging` knows no service.

## Target (M4)

```mermaid
flowchart LR
    client([Clients])
    gw["Gateway (YARP)<br/>route-level strangler switch"]
    ingest[Ingest.Api]
    access["Access.Api<br/>plans → roles → permissions"]
    payments["Payments.Api<br/>CQRS + mediator"]
    legacy["Legacy.Monolith<br/>the before"]
    sql[(SQL Server)]
    redis[(Redis)]
    mq{{RabbitMQ<br/>payment.captured, plan.changed<br/>ingest.completed planned}}
    otel["OpenTelemetry → Aspire dashboard"]

    client --> gw
    gw --> ingest
    gw --> access
    gw --> payments
    gw --> legacy
    legacy --> sql
    ingest --> sql
    access --> sql
    access --> redis
    payments --> sql
    ingest -.->|outbox| mq
    payments -->|outbox| mq
    access -->|outbox| mq
    mq --> access
    mq --> payments
    gw -.->|OTLP, every service| otel

    classDef planned stroke-dasharray: 5 5
```

| Milestone | Adds |
|---|---|
| M1 ✅ | Ingest service, SQL Server, compose, CI |
| M2 ✅ | Access service, JWT, Redis permission cache |
| M3 ✅ | Payments service, mediator, outbox + RabbitMQ, read model, plan.changed |
| M4 ✅ | Legacy monolith, YARP gateway, k6 comparison, OpenTelemetry |

Decisions behind these choices are in [ArggonManager/docs/adr](../ArggonManager/docs/adr/).
