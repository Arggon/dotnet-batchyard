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

### 2026-10-14 @Arggon
Ready for acceptance (Gonzalo): all child tasks are done with evidence (merged PRs, main CI green at 43d80b5 https://github.com/Arggon/dotnet-batchyard/actions/runs/38561095595). The container stays open on purpose: product acceptance is a human decision. To accept, add a comment 'accept: approve' (or 'accept: changes-requested'), then close it. Deliverables: #2 #6 #7; zero-warning build, format check, CodeQL and arggon validate all run in CI.
