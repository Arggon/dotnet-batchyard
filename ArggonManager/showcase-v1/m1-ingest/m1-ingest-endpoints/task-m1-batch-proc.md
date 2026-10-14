---
type: task
status: done
id: task-m1-batch-proc
title: Batch upsert stored procedure with TVP
assignee: Arggon
parent: m1-ingest-endpoints
labels: []
created: "2026-10-14"
updated: "2026-10-14"
---
# Batch upsert stored procedure with TVP

## Context

Versioned SQL schema, the `dbo.ItemBatchRow` table type and `usp_UpsertItemsBatch` (set-based MERGE in one transaction), plus the Dapper repository that calls it. Plan step T2.

## Acceptance

- [x] DbUp migrations under `db/ingest/` create the table, the TVP type and both procedures
- [x] Integration tests (Testcontainers, real SQL Server) cover: create, update, duplicate keys inside one batch, per-row outcome mapping
- [x] No string-built SQL; parameters and TVPs only

## Notes

### 2026-10-14 @Arggon
Done-gate evidence: shipped in PR #3, landed on main via #26 (d7e18fa). main CI green at 43d80b5: ci https://github.com/Arggon/dotnet-batchyard/actions/runs/38561095595 , codeql .../runs/38561095612 , arggon .../runs/38561095609. Acceptance boxes ticked after checking each against main.
