---
type: task
status: done
id: task-m1-demo-client
title: Demo client 50k import
assignee: Arggon
parent: m1-ingest-endpoints
labels: []
created: "2026-10-14"
updated: "2026-10-14"
---
# Demo client 50k import

## Context

The brief asks for a demo client that imports 50,000 items both ways over HTTP, so the benefit is visible end to end, not just in the data layer.

## Acceptance

- [x] `tools/Ingest.DemoClient` imports N items (default 50,000) via `POST /v1/items` and via `POST /v1/items:batch`
- [x] Prints wall time, requests sent and throughput for both
- [x] Measured numbers recorded in `docs/benchmarks.md` with where they were run

## Notes

### 2026-10-14 @Arggon
Done-gate evidence: shipped in PR #8, landed on main via #26 (d7e18fa). main CI green at 43d80b5: ci https://github.com/Arggon/dotnet-batchyard/actions/runs/38561095595 , codeql .../runs/38561095612 , arggon .../runs/38561095609. Acceptance boxes ticked after checking each against main.
