---
plan_id: ingest-batch-001
title: Plan for Ingest batch endpoint
spec: ArggonManager/docs/specs/spec-ingest-batch-001.md
status: proposed
created: 2026-10-14
---

# Plan: Ingest batch endpoint (ingest-batch-001)

Derived from `ArggonManager/docs/specs/spec-ingest-batch-001.md`. Each task carries a
verifiable acceptance criterion and links back to the spec. One PR per task,
stacked in order.

## Tasks

### T1: Solution skeleton and CI (task-m1-scaffold)

- .NET 10 solution, central package management, warnings as errors, architecture tests, CI + CodeQL.
- **Acceptance:** CI is green on the PR with build, format check and tests.

### T2: Schema, TVP and stored procedure (task-m1-batch-proc)

- DbUp migrations in `db/ingest/`; Dapper repository with single and batch upserts.
- **Acceptance:** Testcontainers tests prove create/update, in-batch duplicates and outcome mapping. Depends on T1.

### T3: HTTP endpoints and compose (task-m1-ingest-api)

- Minimal API endpoints, validation, idempotency, Problem Details, Dockerfile + compose.
- **Acceptance:** API integration tests cover the spec's HTTP acceptance items; CI compose smoke test passes. Depends on T2.

### T4: First benchmark (task-m1-benchmark)

- BenchmarkDotNet: N single upserts vs one batch, at 100 / 1,000 / 5,000 rows.
- **Acceptance:** numbers with hardware info in `docs/benchmarks.md` and the README. Depends on T2.
