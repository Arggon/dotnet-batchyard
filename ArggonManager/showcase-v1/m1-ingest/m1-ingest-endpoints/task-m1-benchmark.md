---
type: task
status: done
id: task-m1-benchmark
title: First data-access benchmark
assignee: Arggon
parent: m1-ingest-endpoints
labels: []
created: "2026-10-14"
updated: "2026-10-14"
---
# First data-access benchmark

## Context

Measure, in this repo, one-by-one vs batch upserts at the data-access layer with BenchmarkDotNet. Report whatever ratio comes out. Plan step T4.

## Acceptance

- [x] `benchmarks/Ingest.Benchmarks` runs against SQL Server in Docker
- [x] Results with hardware info committed in `docs/benchmarks.md`
- [x] README shows the measured numbers

## Notes

### 2026-10-14 @Arggon
Done-gate evidence: shipped in PR #5, landed on main via #26 (d7e18fa). main CI green at 43d80b5: ci https://github.com/Arggon/dotnet-batchyard/actions/runs/38561095595 , codeql .../runs/38561095612 , arggon .../runs/38561095609. Acceptance boxes ticked after checking each against main.
