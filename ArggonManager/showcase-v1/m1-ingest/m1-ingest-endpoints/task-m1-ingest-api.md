---
type: task
status: todo
id: task-m1-ingest-api
title: Ingest API single and batch endpoints
parent: m1-ingest-endpoints
labels: []
created: "2026-10-14"
updated: "2026-10-14"
---
# Ingest API single and batch endpoints

## Context

The HTTP surface over the repository: validation, max batch size, partial failure contract, `Idempotency-Key`, Problem Details, and the compose/Dockerfile run path. Plan step T3.

## Acceptance

- [ ] Unit tests for item validation rules
- [ ] API integration tests: single create, batch with mixed valid/invalid rows, batch over the limit -> 400, idempotent replay
- [ ] `docker-compose.yml` + Dockerfile; CI smoke job hits the running container

## Notes
