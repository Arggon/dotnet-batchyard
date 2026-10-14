---
type: task
status: done
id: task-m2-ingest-jwt
title: Tenant and permissions from the token in Ingest
assignee: Arggon
parent: m2-ingest-auth
labels: []
created: "2026-10-14"
updated: "2026-10-14"
---
# Tenant and permissions from the token in Ingest

## Context

Ingest validates the access service's JWT, reads the tenant from it and enforces permissions and the batch limit. Plan step T4.

## Acceptance

- [x] Ingest endpoints require a valid token; missing/invalid -> 401, missing permission -> 403
- [x] Batch limit = min(service limit, token `max_batch`)
- [x] Ingest authorization matrix built from the real role catalog
- [x] Smoke script, demo client and k6 fetch a token first; README updated

## Notes

### 2026-10-14 @Arggon
Done-gate evidence: shipped in PR #14, landed on main via #26 (d7e18fa). main CI green at 43d80b5: ci https://github.com/Arggon/dotnet-batchyard/actions/runs/38561095595 , codeql .../runs/38561095612 , arggon .../runs/38561095609. Acceptance boxes ticked after checking each against main.
