---
type: task
status: done
id: task-m4-legacy
title: Legacy monolith
parent: m4-extraction
labels: []
created: "2026-10-14"
updated: "2026-10-14"
---
# Legacy monolith

## Context

Legacy.Monolith: an old-style ASP.NET Core app serving /v1/items with one SELECT and one INSERT/UPDATE per row, on the shared items table. Plan step T1.

## Acceptance

- [x] Same request/response contract as ingest-svc for /v1/items and /v1/items:batch
- [x] A legacy-only route stays in the monolith (stock report)
- [x] Integration tests against real SQL Server

## Notes

### 2026-10-14 @Arggon
Done-gate evidence: shipped in PR #21, landed on main via #26 (d7e18fa). main CI green at 43d80b5: ci https://github.com/Arggon/dotnet-batchyard/actions/runs/38561095595 , codeql .../runs/38561095612 , arggon .../runs/38561095609. Acceptance boxes ticked after checking each against main.
