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

- [ ] benchmarks/k6/strangler.js
- [ ] Measured results with environment in docs/benchmarks.md
- [ ] On-demand workflow job

## Notes
