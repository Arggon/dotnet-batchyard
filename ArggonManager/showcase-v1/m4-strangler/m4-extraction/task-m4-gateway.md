---
type: task
status: done
id: task-m4-gateway
title: YARP gateway with per-route switching
parent: m4-extraction
labels: []
created: "2026-10-14"
updated: "2026-10-14"
---
# YARP gateway with per-route switching

## Context

Gateway: YARP routes for access, payments, items and legacy reports. Each item route points at the legacy or ingest cluster through one config key (ADR 0014). Plan step T2.

## Acceptance

- [x] Flipping one config key moves a route; tests prove it with fake backends
- [x] X-Backend response header names the cluster that answered
- [x] Gateway in compose; smoke test goes through it

## Notes

### 2026-10-14 @Arggon
Done-gate evidence: shipped in PR #22, landed on main via #26 (d7e18fa). main CI green at 43d80b5: ci https://github.com/Arggon/dotnet-batchyard/actions/runs/38561095595 , codeql .../runs/38561095612 , arggon .../runs/38561095609. Acceptance boxes ticked after checking each against main.
