# dotnet-batchyard

[![ci](https://github.com/Arggon/dotnet-batchyard/actions/workflows/ci.yml/badge.svg)](https://github.com/Arggon/dotnet-batchyard/actions/workflows/ci.yml)
[![codeql](https://github.com/Arggon/dotnet-batchyard/actions/workflows/codeql.yml/badge.svg)](https://github.com/Arggon/dotnet-batchyard/actions/workflows/codeql.yml)

A small .NET 10 backend for an invented B2B parts marketplace. It shows, in code you can read in about 15 minutes, the backend work on my CV: batch ingestion over SQL Server, permission models, CQRS payments and pulling a service out of a monolith.

I'm Gonzalo, a senior .NET backend engineer. [Portfolio and CV](https://arggon.github.io/arggon-cv/)

## The headline number

Inserting 5,000 items through the batch endpoint's data path takes **155 ms**. Doing it one item at a time takes **7.6 s**, so the batch path saves **98%** of the time. That number was measured in this repo with BenchmarkDotNet. [Full results and caveats](docs/benchmarks.md)

| Items | one-by-one | batch (TVP + MERGE) | Time saved |
|------:|-----------:|--------------------:|-----------:|
| 100   | 163 ms     | 8 ms                | 95%        |
| 1,000 | 1,357 ms   | 33 ms               | 98%        |
| 5,000 | 7,551 ms   | 155 ms              | 98%        |

## What it shows

| On my CV | In this repo | Status |
|---|---|---|
| Batch endpoint + stored procedure that cut client processing time | `POST /v1/items:batch`: validates in memory, sends one table-valued parameter to a stored procedure, upserts with one set-based `MERGE` | ✅ M1 |
| Spec-first, test-gated delivery with AI agents | Every change starts as a spec and a task under [`ArggonManager/`](ArggonManager/), with tests written first and one small PR per task | ✅ M1 |
| Access roles and permissions that enabled marketplace monetization | Plans → roles → permissions, JWT, authorization matrix tests | 🔜 M2 |
| CQRS + mediator in a payments service | Commands and queries, a hand-rolled mediator, idempotent payments, outbox + RabbitMQ | 🔜 M3 |
| A .NET microservice extracted from a monolith | YARP gateway moving routes from a legacy app, compared with k6 | 🔜 M4 |

At work these patterns ran on .NET 9. Here they're rebuilt from scratch on .NET 10 with an invented domain, so there's no client code or data.

## Run it

You only need Docker:

```bash
docker compose up --build
```

Then send a batch with one valid and one invalid row:

```bash
curl -s -X POST localhost:8080/v1/items:batch \
  -H "Content-Type: application/json" \
  -H "X-Tenant-Id: 3f2c6a7e-1d4b-4c8e-9a51-2b7d0e6f9c13" \
  -d '{"items":[
        {"sku":"BRK-001","name":"Brake pad","price":12.5,"currency":"USD","quantity":40},
        {"sku":"FLT-002","name":"Oil filter","price":-1,"currency":"USD","quantity":3}]}'
```

```json
{ "created": 1, "updated": 0, "rejected": 1,
  "results": [
    { "index": 0, "sku": "BRK-001", "outcome": "created" },
    { "index": 1, "sku": "FLT-002", "outcome": "rejected", "reason": "price must be zero or more" } ] }
```

The OpenAPI document is at `http://localhost:8080/openapi/v1.json`.

## How the batch path works

```mermaid
flowchart LR
    C[Client] -->|POST /v1/items:batch<br/>up to 5,000 rows| API[Ingest.Api<br/>validate in memory]
    API -->|invalid rows| R[rejected + reason]
    API -->|valid rows as one TVP| P[usp_UpsertItemsBatch]
    P -->|one MERGE,<br/>one transaction| DB[(SQL Server<br/>dbo.Items)]
    P -->|OUTPUT $action per row| API
```

- **Partial failure, never partial corruption.** Bad rows are rejected with a reason and good rows commit. A database error rolls back the whole batch.
- **Duplicates inside one batch** are handled in SQL: the first one wins and the rest are reported, so `MERGE` never fails on them.
- **Safe retries.** Send an `Idempotency-Key` header and a retried request gets the original response back. Reusing the key with a different body returns 409.
- **The SQL is code.** Tables, the TVP type and the procedures live in [`db/ingest/`](db/ingest/) and are applied in order by DbUp.

Why it's built this way: [ADR 0001: TVP + MERGE](ArggonManager/docs/adr/0001-batch-upserts-via-tvp-and-merge.md) · [all ADRs](ArggonManager/docs/adr/)

## Tests and quality gates

- **Unit tests** for the validation rules
- **Integration tests against a real SQL Server 2022** started by Testcontainers, with no database mocks: the stored procedure, the TVP, duplicates, a 5,000-row batch, and the HTTP endpoints end to end
- **Architecture tests** (NetArchTest): the data layer can't depend on the API
- **CI on every PR:** build with warnings as errors, `dotnet format` check, all tests, a `docker compose` smoke test, CodeQL and `arggon validate`

```bash
dotnet test   # needs Docker for the integration tests
```

## Stack

.NET 10 · ASP.NET Core Minimal APIs · SQL Server 2022 · Dapper · DbUp · xUnit + Shouldly · Testcontainers · BenchmarkDotNet · Docker Compose · GitHub Actions

## How it was built

This repo is built with [ArggonManager](https://github.com/Arggon/ArggonManager), a method and CLI I designed for shipping software with AI agents. Agents plan, implement, review and verify; I'm the product owner. In practice:

- **Agents wrote most of the code, tests and docs.**
- **I designed the method, wrote or approved the specs and ADRs, review every PR and decide what gets merged.**

Each feature goes spec → plan → tasks → test-first commits → review → merge. The trail is in [`ArggonManager/`](ArggonManager/) and in the PR history. More in [docs/agentic-workflow.md](docs/agentic-workflow.md).

## Repo map

```
src/Ingest.Api          Minimal API: endpoints, validation, idempotency
src/Ingest.Data         Dapper repository + DbUp migrator
src/BuildingBlocks      Small shared helpers (no framework)
db/ingest               Versioned SQL: tables, TVP type, stored procedures
tests/                  Unit, integration (Testcontainers) and architecture tests
benchmarks/             BenchmarkDotNet
ArggonManager/          Specs, plans, tasks, ADRs, playbooks
docs/                   Benchmarks, agentic workflow
```

## Roadmap

- **M1, done:** skeleton, CI, compose, ingest batch endpoint, integration tests, first benchmark
- **M2:** access service with plans, roles and permissions, JWT, authorization matrix, Redis cache
- **M3:** payments service with CQRS, a hand-rolled mediator, idempotency, outbox + RabbitMQ
- **M4:** legacy monolith + YARP strangler + k6, OpenTelemetry

## License

[MIT](LICENSE)
