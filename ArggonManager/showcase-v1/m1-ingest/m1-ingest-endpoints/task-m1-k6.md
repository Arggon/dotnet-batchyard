---
type: task
status: done
id: task-m1-k6
title: k6 end-to-end load test
assignee: Arggon
parent: m1-ingest-endpoints
labels: []
created: "2026-10-14"
updated: "2026-10-14"
---
# k6 end-to-end load test

## Context

End-to-end latency under concurrent load, the same tool M4 will use for the monolith comparison.

## Acceptance

- [x] `benchmarks/k6/ingest.js` drives single and batch scenarios with thresholds
- [x] Runnable in CI on demand against `docker compose`, summary in the job page
- [x] Numbers in `docs/benchmarks.md` with their source

## Notes

### 2026-10-14 @Arggon
Done-gate evidence: shipped in PR #9, landed on main via #26 (d7e18fa). main CI green at 43d80b5: ci https://github.com/Arggon/dotnet-batchyard/actions/runs/38561095595 , codeql .../runs/38561095612 , arggon .../runs/38561095609. Acceptance boxes ticked after checking each against main.
