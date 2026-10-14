---
type: task
status: todo
id: task-m1-batch-proc
title: Batch upsert stored procedure with TVP
parent: m1-ingest-endpoints
labels: []
created: "2026-10-14"
updated: "2026-10-14"
---
# Batch upsert stored procedure with TVP

## Context

Versioned SQL schema, the `dbo.ItemBatchRow` table type and `usp_UpsertItemsBatch` (set-based MERGE in one transaction), plus the Dapper repository that calls it. Plan step T2.

## Acceptance

- [ ] DbUp migrations under `db/ingest/` create the table, the TVP type and both procedures
- [ ] Integration tests (Testcontainers, real SQL Server) cover: create, update, duplicate keys inside one batch, per-row outcome mapping
- [ ] No string-built SQL; parameters and TVPs only

## Notes
