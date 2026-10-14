---
type: story
status: todo
id: m1-ingest-endpoints
title: Ingest single and batch endpoints
parent: m1-ingest
labels: []
created: "2026-10-14"
updated: "2026-10-14"
---
# Ingest single and batch endpoints

## Context

The centerpiece: `POST /v1/items` and `POST /v1/items:batch`, backed by Dapper and versioned SQL (DbUp), with a partial-failure contract and idempotency keys.

## Acceptance

- [ ] All acceptance items in `spec-ingest-batch-001` ticked with evidence
- [ ] `docker compose up` starts SQL Server + ingest-api from a clean clone

## Notes
