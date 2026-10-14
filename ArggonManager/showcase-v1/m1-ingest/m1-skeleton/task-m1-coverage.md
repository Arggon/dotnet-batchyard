---
type: task
status: done
id: task-m1-coverage
title: Coverage reported in CI
assignee: Arggon
parent: m1-skeleton
labels: []
created: "2026-10-14"
updated: "2026-10-14"
---
# Coverage reported in CI

## Context

Coverage must be visible on every PR (reported, not gated, per the brief's quality bar).

## Acceptance

- [x] CI merges coverage from all test projects and writes a summary to the job page
- [x] HTML report uploaded as an artifact
- [x] Test and benchmark assemblies excluded from the numbers

## Notes

### 2026-10-14 @Arggon
Done-gate evidence: shipped in PR #6 (coverage gating fixed in #27), landed on main via #26 (d7e18fa). main CI green at 43d80b5: ci https://github.com/Arggon/dotnet-batchyard/actions/runs/38561095595 , codeql .../runs/38561095612 , arggon .../runs/38561095609. Acceptance boxes ticked after checking each against main.
