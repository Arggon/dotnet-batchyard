---
type: story
status: todo
id: m1-skeleton
title: Solution skeleton and CI
parent: m1-ingest
labels: []
created: "2026-10-14"
updated: "2026-10-14"
---
# Solution skeleton and CI

## Context

Everything later work stands on: solution layout, central package management, warnings as errors, and a CI gate that builds, checks formatting, tests and runs CodeQL.

## Acceptance

- [ ] `dotnet build` passes with zero warnings (TreatWarningsAsErrors on)
- [ ] `dotnet test` green locally and in GitHub Actions
- [ ] `dotnet format --verify-no-changes` enforced in CI
- [ ] CodeQL runs on pull requests
- [ ] `arggon validate` runs in CI

## Notes
