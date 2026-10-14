---
playbook_id: ef-core
version: 10
researched: 2026-10-14
status: current
---

# EF Core playbook (ef-core)

Technology playbook: the chosen version and the current best practices for
ef-core. The version/best-practices research happened when this file was
created — cite dated sources (URL + access date) in every section so the next
reader can re-verify, and keep this file current via
`arggon playbook status`.

## Setup

- `Microsoft.EntityFrameworkCore.SqlServer` 10.x, matching the runtime. Source: https://learn.microsoft.com/ef/core/what-is-new/ef-core-10.0/whatsnew (accessed 2026-10-14).

## Conventions

- Used for Access CRUD only (ADR 0002). Schema comes from DbUp scripts (ADR 0007): no EF migrations, `ToTable` mappings match the SQL.
- `AsNoTracking` for reads; one `SaveChangesAsync` per use case.

## Testing

- Integration tests against real SQL Server; no in-memory provider (it hides SQL behavior).

## Security

- LINQ queries are parameterized; raw SQL only through `FromSql` with interpolated parameters, never string concatenation.

## Upgrade policy

- Upgrade with each .NET major; refresh with `arggon playbook refresh ef-core --version <v>`.
