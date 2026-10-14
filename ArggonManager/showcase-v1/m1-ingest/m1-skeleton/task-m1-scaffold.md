---
type: task
status: done
id: task-m1-scaffold
title: "Scaffold solution, build props and CI workflow"
assignee: Arggon
parent: m1-skeleton
labels: []
created: "2026-10-14"
updated: "2026-10-14"
---
# Scaffold solution, build props and CI workflow

## Context

Create the .NET 10 solution with the repo layout from the brief (src/, tests/, benchmarks/, db/) and the CI workflow. Plan step T1.

## Acceptance

- [x] `dotnet-batchyard.slnx` with BuildingBlocks, Ingest.Api, Ingest.Data, unit, integration and architecture test projects
- [x] `Directory.Build.props` (net10.0, nullable, latest C#, warnings as errors) and `Directory.Packages.props`
- [x] Architecture test: Ingest.Data does not depend on Ingest.Api
- [x] `.github/workflows/ci.yml` (build, format, test, coverage artifact) and `codeql.yml` green on the PR

## Notes

### 2026-10-14 @Arggon
Done-gate evidence: shipped in PR #2, landed on main via #26 (d7e18fa). main CI green at 43d80b5: ci https://github.com/Arggon/dotnet-batchyard/actions/runs/38561095595 , codeql .../runs/38561095612 , arggon .../runs/38561095609. Acceptance boxes ticked after checking each against main.
