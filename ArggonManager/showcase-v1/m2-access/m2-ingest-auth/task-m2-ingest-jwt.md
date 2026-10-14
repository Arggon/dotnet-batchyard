---
type: task
status: todo
id: task-m2-ingest-jwt
title: Tenant and permissions from the token in Ingest
parent: m2-ingest-auth
labels: []
created: "2026-10-14"
updated: "2026-10-14"
---
# Tenant and permissions from the token in Ingest

## Context

Ingest validates the access service's JWT, reads the tenant from it and enforces permissions and the batch limit. Plan step T4.

## Acceptance

- [ ] Ingest endpoints require a valid token; missing/invalid -> 401, missing permission -> 403
- [ ] Batch limit = min(service limit, token `max_batch`)
- [ ] Ingest authorization matrix built from the real role catalog
- [ ] Smoke script, demo client and k6 fetch a token first; README updated

## Notes
