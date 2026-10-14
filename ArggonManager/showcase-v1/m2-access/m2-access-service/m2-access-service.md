---
type: story
status: todo
id: m2-access-service
title: Access service
parent: m2-access
labels: []
created: "2026-10-14"
updated: "2026-10-14"
---
# Access service

## Context

The access service owns tenants, plans, roles, permissions and assignments, and issues the JWTs every other service trusts.

## Acceptance

- [ ] All access acceptance items in `spec-access-plans-002` ticked with evidence
- [ ] Authorization matrix tests (role x endpoint -> status) are data-driven xUnit theories

## Notes

### 2026-10-14 @Arggon
Ready for acceptance (Gonzalo): all child tasks are done with evidence (merged PRs, main CI green at 43d80b5 https://github.com/Arggon/dotnet-batchyard/actions/runs/38561095595). The container stays open on purpose: product acceptance is a human decision. To accept, add a comment 'accept: approve' (or 'accept: changes-requested'), then close it. Deliverables: #11 #12 #13. Open note: spec-access-plans-002 is still 'proposed'.
