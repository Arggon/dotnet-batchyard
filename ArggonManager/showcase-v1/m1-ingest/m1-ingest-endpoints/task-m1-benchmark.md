---
type: task
status: todo
id: task-m1-benchmark
title: First data-access benchmark
parent: m1-ingest-endpoints
labels: []
created: "2026-10-14"
updated: "2026-10-14"
---
# First data-access benchmark

## Context

Measure, in this repo, one-by-one vs batch upserts at the data-access layer with BenchmarkDotNet. Report whatever ratio comes out. Plan step T4.

## Acceptance

- [ ] `benchmarks/Ingest.Benchmarks` runs against SQL Server in Docker
- [ ] Results with hardware info committed in `docs/benchmarks.md`
- [ ] README shows the measured numbers

## Notes
