---
type: task
status: todo
id: task-m1-k6
title: k6 end-to-end load test
parent: m1-ingest-endpoints
labels: []
created: "2026-10-14"
updated: "2026-10-14"
---
# k6 end-to-end load test

## Context

End-to-end latency under concurrent load, the same tool M4 will use for the monolith comparison.

## Acceptance

- [ ] `benchmarks/k6/ingest.js` drives single and batch scenarios with thresholds
- [ ] Runnable in CI on demand against `docker compose`, summary in the job page
- [ ] Numbers in `docs/benchmarks.md` with their source

## Notes
