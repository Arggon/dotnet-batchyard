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
