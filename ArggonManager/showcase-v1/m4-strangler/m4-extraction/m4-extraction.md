---
type: story
status: todo
id: m4-extraction
title: Extract ingest from the monolith
parent: m4-strangler
labels: []
created: "2026-10-14"
updated: "2026-10-14"
---
# Extract ingest from the monolith

## Context

The legacy monolith serves items with per-row queries. The gateway sends item routes to it or to ingest-svc depending on one config value per route, and k6 measures both.

## Acceptance

- [ ] Legacy monolith with the same item contract
- [ ] Gateway routes switchable per route, covered by tests
- [ ] k6 p50/p95 comparison committed in docs/benchmarks.md

## Notes

### 2026-10-14 @Arggon
Ready for acceptance (Gonzalo): all child tasks are done with evidence (merged PRs, main CI green at 43d80b5 https://github.com/Arggon/dotnet-batchyard/actions/runs/38561095595). The container stays open on purpose: product acceptance is a human decision. To accept, add a comment 'accept: approve' (or 'accept: changes-requested'), then close it. Deliverables: #21 #22 #23; k6 p50/p95 in docs/benchmarks.md. Open note: spec-strangler-004 is still 'proposed'.
