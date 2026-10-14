---
type: task
status: done
id: task-m4-k6-compare
title: "k6: monolith vs service"
parent: m4-extraction
labels: []
created: "2026-10-14"
updated: "2026-10-14"
---
# k6: monolith vs service

## Context

k6 script that runs the same batch load against the legacy route and the ingest route, reporting p50/p95 and items per second. Plan step T3.

## Acceptance

- [x] benchmarks/k6/strangler.js
- [x] Measured results with environment in docs/benchmarks.md
- [x] On-demand workflow job

## Notes

### 2026-10-14 @Arggon
Done-gate evidence: shipped in PR #23, landed on main via #26 (d7e18fa). main CI green at 43d80b5: ci https://github.com/Arggon/dotnet-batchyard/actions/runs/38561095595 , codeql .../runs/38561095612 , arggon .../runs/38561095609. Acceptance boxes ticked after checking each against main.
