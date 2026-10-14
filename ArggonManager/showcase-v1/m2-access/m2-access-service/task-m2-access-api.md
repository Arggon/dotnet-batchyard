---
type: task
status: todo
id: task-m2-access-api
title: Access API with JWT login
parent: m2-access-service
labels: []
created: "2026-10-14"
updated: "2026-10-14"
---
# Access API with JWT login

## Context

The HTTP surface: token endpoint, tenant info, plan change, marketplace webhook stub, permission filters, rate limiting. Plan step T2.

## Acceptance

- [ ] `POST /v1/auth/token` (client credentials) returns a signed JWT; rate limited
- [ ] `[RequirePermission]`-style endpoint filter; matrix tests for access endpoints
- [ ] Marketplace webhook stub grants the marketplace-seller role
- [ ] `SECURITY.md` updated

## Notes
