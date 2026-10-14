---
type: task
status: done
id: task-m1-ingest-api
title: Ingest API single and batch endpoints
assignee: Arggon
parent: m1-ingest-endpoints
labels: []
created: "2026-10-14"
updated: "2026-10-14"
---
# Ingest API single and batch endpoints

## Context

The HTTP surface over the repository: validation, max batch size, partial failure contract, `Idempotency-Key`, Problem Details, and the compose/Dockerfile run path. Plan step T3.

## Acceptance

- [x] Unit tests for item validation rules
- [x] API integration tests: single create, batch with mixed valid/invalid rows, batch over the limit -> 400, idempotent replay
- [x] `docker-compose.yml` + Dockerfile; CI smoke job hits the running container

## Notes

### 2026-10-14 @Arggon
Done-gate evidence: shipped in PR #4, landed on main via #26 (d7e18fa). main CI green at 43d80b5: ci https://github.com/Arggon/dotnet-batchyard/actions/runs/38561095595 , codeql .../runs/38561095612 , arggon .../runs/38561095609. Acceptance boxes ticked after checking each against main.
