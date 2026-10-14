---
type: task
status: done
id: task-m3-mediator
title: "Mediator, commands and queries"
parent: m3-payments-service
labels: []
created: "2026-10-14"
updated: "2026-10-14"
---
# Mediator, commands and queries

## Context

The hand-written mediator (ADR 0010), pipeline behaviors and the command/query handlers, unit-tested with in-memory fakes. Plan step T1.

## Acceptance

- [x] Mediator (~60 lines) with validation, logging and transaction behaviors
- [x] CreateInvoice, CapturePayment, RefundPayment, GetInvoice handlers with unit tests
- [x] Money as decimal + currency

## Notes

### 2026-10-14 @Arggon
Done-gate evidence: shipped in PR #16, landed on main via #26 (d7e18fa). main CI green at 43d80b5: ci https://github.com/Arggon/dotnet-batchyard/actions/runs/38561095595 , codeql .../runs/38561095612 , arggon .../runs/38561095609. Acceptance boxes ticked after checking each against main.
