---
type: task
status: done
id: task-m4-gateway
title: "YARP gateway with per-route switching"
parent: m4-extraction
labels: []
created: "2026-10-14"
updated: "2026-10-14"
---
# YARP gateway with per-route switching

## Context

Gateway: YARP routes for access, payments, items and legacy reports. Each item route points at the legacy or ingest cluster through one config key (ADR 0014). Plan step T2.

## Acceptance

- [ ] Flipping one config key moves a route; tests prove it with fake backends
- [ ] X-Backend response header names the cluster that answered
- [ ] Gateway in compose; smoke test goes through it

## Notes
