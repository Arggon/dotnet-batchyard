---
type: story
status: todo
id: m4-extraction
title: "Extract ingest from the monolith"
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
