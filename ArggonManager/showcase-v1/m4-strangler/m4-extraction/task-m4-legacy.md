---
type: task
status: done
id: task-m4-legacy
title: "Legacy monolith"
parent: m4-extraction
labels: []
created: "2026-10-14"
updated: "2026-10-14"
---
# Legacy monolith

## Context

Legacy.Monolith: an old-style ASP.NET Core app serving /v1/items with one SELECT and one INSERT/UPDATE per row, on the shared items table. Plan step T1.

## Acceptance

- [ ] Same request/response contract as ingest-svc for /v1/items and /v1/items:batch
- [ ] A legacy-only route stays in the monolith (stock report)
- [ ] Integration tests against real SQL Server

## Notes
