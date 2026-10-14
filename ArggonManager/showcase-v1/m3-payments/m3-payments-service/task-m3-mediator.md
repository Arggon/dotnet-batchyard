---
type: task
status: todo
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

- [ ] Mediator (~60 lines) with validation, logging and transaction behaviors
- [ ] CreateInvoice, CapturePayment, RefundPayment, GetInvoice handlers with unit tests
- [ ] Money as decimal + currency

## Notes
