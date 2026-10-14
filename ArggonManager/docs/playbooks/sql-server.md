---
playbook_id: sql-server
version: 2022
researched: 2026-10-14
status: current
---

# SQL Server playbook (sql-server)

Technology playbook: the chosen version and the current best practices for
sql-server. The version/best-practices research happened when this file was
created — cite dated sources (URL + access date) in every section so the next
reader can re-verify, and keep this file current via
`arggon playbook status`.

## Setup

- `mcr.microsoft.com/mssql/server:2022-latest` (Developer edition, free for dev/test) in Docker for local runs, tests and benchmarks. Source: https://learn.microsoft.com/sql/linux/quickstart-install-connect-docker (accessed 2026-10-14).
- Client: `Microsoft.Data.SqlClient` 6.x; connection strings use `TrustServerCertificate=True` for local containers only.

## Conventions

- Schema is versioned plain SQL in `db/<service>/NNNN_description.sql`, applied by DbUp, never edited after merge.
- Procedures: `SET NOCOUNT ON; SET XACT_ABORT ON;`, explicit transactions, `usp_` prefix.
- Bulk input goes through table-valued parameters; `MERGE` sources must have unique keys (dedupe first) and use `HOLDLOCK` to avoid upsert races. Source: https://learn.microsoft.com/sql/t-sql/statements/merge-transact-sql (accessed 2026-10-14).
- Money is `decimal(19,4)` plus a `char(3)` currency.

## Testing

- Testcontainers.MsSql spins one container per test assembly; each test uses its own tenant id instead of truncating tables.

## Security

- Parameterized queries and TVPs only; no dynamic SQL built from input.
- The `sa` login is used only in throwaway containers; a real deployment would use a least-privilege login with EXECUTE on the procedures.

## Upgrade policy

- Re-research on stale status or when SQL Server 2025 becomes the default image; refresh with `arggon playbook refresh sql-server --version <v>`.
