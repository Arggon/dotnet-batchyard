---
type: task
status: done
id: task-m2-access-data
title: Access data model and plan changes
assignee: Arggon
parent: m2-access-service
labels: []
created: "2026-10-14"
updated: "2026-10-14"
---
# Access data model and plan changes

## Context

Schema and reference data (plans, roles, permissions) as versioned SQL, EF Core for reads and writes, and the plan-change rules. Plan step T1.

## Acceptance

- [x] `db/access/` migrations: tables plus seeded plans, roles and permissions
- [x] EF Core `AccessDbContext`; effective permissions and batch limit resolved per client
- [x] Changing the plan swaps the plan-granted role assignments and keeps tenant-admin
- [x] Integration tests against real SQL Server

## Notes

### 2026-10-14 @Arggon
Done-gate evidence: shipped in PR #11, landed on main via #26 (d7e18fa). main CI green at 43d80b5: ci https://github.com/Arggon/dotnet-batchyard/actions/runs/38561095595 , codeql .../runs/38561095612 , arggon .../runs/38561095609. Acceptance boxes ticked after checking each against main.
