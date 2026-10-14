---
type: task
status: todo
id: task-m1-scaffold
title: "Scaffold solution, build props and CI workflow"
parent: m1-skeleton
labels: []
created: "2026-10-14"
updated: "2026-10-14"
---
# Scaffold solution, build props and CI workflow

## Context

Create the .NET 10 solution with the repo layout from the brief (src/, tests/, benchmarks/, db/) and the CI workflow. Plan step T1.

## Acceptance

- [ ] `dotnet-batchyard.slnx` with BuildingBlocks, Ingest.Api, Ingest.Data, unit, integration and architecture test projects
- [ ] `Directory.Build.props` (net10.0, nullable, latest C#, warnings as errors) and `Directory.Packages.props`
- [ ] Architecture test: Ingest.Data does not depend on Ingest.Api
- [ ] `.github/workflows/ci.yml` (build, format, test, coverage artifact) and `codeql.yml` green on the PR

## Notes
