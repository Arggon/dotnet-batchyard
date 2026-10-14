---
type: task
status: done
id: task-m2-access-api
title: Access API with JWT login
assignee: Arggon
parent: m2-access-service
labels: []
created: "2026-10-14"
updated: "2026-10-14"
---
# Access API with JWT login

## Context

The HTTP surface: token endpoint, tenant info, plan change, marketplace webhook stub, permission filters, rate limiting. Plan step T2.

## Acceptance

- [x] `POST /v1/auth/token` (client credentials) returns a signed JWT; rate limited
- [x] `[RequirePermission]`-style endpoint filter; matrix tests for access endpoints
- [x] Marketplace webhook stub grants the marketplace-seller role
- [x] `SECURITY.md` updated

## Notes

### 2026-10-14 @Arggon
Done-gate evidence: shipped in PR #12, landed on main via #26 (d7e18fa). main CI green at 43d80b5: ci https://github.com/Arggon/dotnet-batchyard/actions/runs/38561095595 , codeql .../runs/38561095612 , arggon .../runs/38561095609. Acceptance boxes ticked after checking each against main.
