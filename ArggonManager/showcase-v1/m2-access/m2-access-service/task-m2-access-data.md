---
type: task
status: todo
id: task-m2-access-data
title: Access data model and plan changes
parent: m2-access-service
labels: []
created: "2026-10-14"
updated: "2026-10-14"
---
# Access data model and plan changes

## Context

Schema and reference data (plans, roles, permissions) as versioned SQL, EF Core for reads and writes, and the plan-change rules. Plan step T1.

## Acceptance

- [ ] `db/access/` migrations: tables plus seeded plans, roles and permissions
- [ ] EF Core `AccessDbContext`; effective permissions and batch limit resolved per client
- [ ] Changing the plan swaps the plan-granted role assignments and keeps tenant-admin
- [ ] Integration tests against real SQL Server

## Notes
