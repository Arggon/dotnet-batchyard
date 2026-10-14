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

### 2026-10-14 @Arggon
Ready for acceptance (Gonzalo): all child tasks are done with evidence (merged PRs, main CI green at 43d80b5 https://github.com/Arggon/dotnet-batchyard/actions/runs/38561095595). The container stays open on purpose: product acceptance is a human decision. To accept, add a comment 'accept: approve' (or 'accept: changes-requested'), then close it. Deliverables: #3 #4 #5 #8 #9. Open note: spec-ingest-batch-001 is still status 'proposed'; its acceptance items are not ticked on the spec itself yet.
