---
type: story
status: todo
id: m3-payments-service
title: Payments service
parent: m3-payments
labels: []
created: "2026-10-14"
updated: "2026-10-14"
---
# Payments service

## Context

Invoices and payments with commands and queries split through a hand-written mediator, idempotent capture and a resilient fake gateway.

## Acceptance

- [ ] All payments acceptance items in `spec-payments-003` ticked with evidence

## Notes

### 2026-10-14 @Arggon
Ready for acceptance (Gonzalo): all child tasks are done with evidence (merged PRs, main CI green at 43d80b5 https://github.com/Arggon/dotnet-batchyard/actions/runs/38561095595). The container stays open on purpose: product acceptance is a human decision. To accept, add a comment 'accept: approve' (or 'accept: changes-requested'), then close it. Deliverables: #16 #17. Open note: spec-payments-003 is still 'proposed'.
