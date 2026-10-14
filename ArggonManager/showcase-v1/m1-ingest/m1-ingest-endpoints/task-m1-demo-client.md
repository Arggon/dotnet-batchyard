---
type: task
status: todo
id: task-m1-demo-client
title: Demo client 50k import
parent: m1-ingest-endpoints
labels: []
created: "2026-10-14"
updated: "2026-10-14"
---
# Demo client 50k import

## Context

The brief asks for a demo client that imports 50,000 items both ways over HTTP, so the benefit is visible end to end, not just in the data layer.

## Acceptance

- [ ] `tools/Ingest.DemoClient` imports N items (default 50,000) via `POST /v1/items` and via `POST /v1/items:batch`
- [ ] Prints wall time, requests sent and throughput for both
- [ ] Measured numbers recorded in `docs/benchmarks.md` with where they were run

## Notes
