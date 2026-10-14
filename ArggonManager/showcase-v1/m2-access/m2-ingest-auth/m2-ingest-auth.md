---
type: story
status: todo
id: m2-ingest-auth
title: Ingest secured by JWT
parent: m2-access
labels: []
created: "2026-10-14"
updated: "2026-10-14"
---
# Ingest secured by JWT

## Context

Ingest stops trusting a header and takes the tenant and permissions from a signed token (supersedes ADR 0004 with ADR 0009).

## Acceptance

- [ ] `X-Tenant-Id` removed; tenant comes from the `tenant_id` claim
- [ ] Ingest endpoints enforce permissions and the plan's batch limit

## Notes

### 2026-10-14 @Arggon
Ready for acceptance (Gonzalo): all child tasks are done with evidence (merged PRs, main CI green at 43d80b5 https://github.com/Arggon/dotnet-batchyard/actions/runs/38561095595). The container stays open on purpose: product acceptance is a human decision. To accept, add a comment 'accept: approve' (or 'accept: changes-requested'), then close it. Deliverables: #14 (X-Tenant-Id removed, tenant from the JWT claim, batch limit = min(service, max_batch)).
