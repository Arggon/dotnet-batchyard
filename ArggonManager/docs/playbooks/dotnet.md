---
playbook_id: dotnet
version: 10
researched: 2026-10-14
status: current
---

# .NET playbook (dotnet)

Technology playbook: the chosen version and the current best practices for
dotnet. The version/best-practices research happened when this file was
created — cite dated sources (URL + access date) in every section so the next
reader can re-verify, and keep this file current via
`arggon playbook status`.

## Setup

- SDK 10.0 (LTS, supported until Nov 2028). CI uses `actions/setup-dotnet` with `global.json` (`rollForward: latestFeature`). Source: https://dotnet.microsoft.com/platform/support/policy/dotnet-core (accessed 2026-10-14).
- Restore/build/test: `dotnet build -c Release` then `dotnet test -c Release`.

## Conventions

- `Directory.Build.props`: `net10.0`, `Nullable` enable, `LangVersion` latest, `TreatWarningsAsErrors` true, analyzers on.
- Central package management in `Directory.Packages.props`; no versions in project files.
- Minimal APIs grouped per feature (`MapGroup`), Problem Details for every error (`AddProblemDetails`). Source: https://learn.microsoft.com/aspnet/core/fundamentals/minimal-apis (accessed 2026-10-14).
- Formatting is `.editorconfig` + `dotnet format --verify-no-changes` in CI.

## Testing

- xUnit 2.9 + Shouldly (BSD-3) for asserts; FluentAssertions 8+ is commercially licensed and not used.
- Integration tests use Testcontainers for .NET against real SQL Server; no database mocks. Source: https://dotnet.testcontainers.org/modules/mssql/ (accessed 2026-10-14).
- API tests use `WebApplicationFactory<Program>`.
- Coverage via coverlet collector, uploaded as a CI artifact (reported, not gated).

## Security

- No secrets in the repo; local passwords live only in `docker-compose.yml` for throwaway containers and are marked as such.
- CodeQL on every PR. `dotnet list package --vulnerable` before releases.

## Upgrade policy

- Re-research when `arggon playbook status` reports stale (90 days) or at the next .NET release (November). Update Setup and Conventions first, then refresh with `arggon playbook refresh dotnet --version <v>`.
