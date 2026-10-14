---
spec_id: payments-003
title: "Payments: CQRS, mediator and events"
status: proposed
created: 2026-10-14
---

# Spec: Payments: CQRS, mediator and events (payments-003)

## Purpose

Tenants pay invoices. A payment capture must never charge twice, even with
client retries and concurrent requests, and other parts of the system must learn
about payments reliably. This is the generic version of the CQRS + Mediator
payments microservice on the CV.

Invariants:

- **One charge per idempotency key.** A unique index enforces it in SQL; the
  gateway call carries the same key.
- **Commands change state, queries never do.** Every command goes through the
  mediator's pipeline (validation, logging, one transaction).
- **No lost or invented events.** Events are written to an outbox table in the
  same transaction as the change and published afterwards (at least once).
- **Consumers are idempotent.** Each consumer records handled message ids.
- **Money is `decimal(19,4)` plus an ISO 4217 currency.** No floating point.

## Synopsis

```http
POST /v1/invoices                         { "amount": 120.00, "currency": "USD", "reference": "PO-1" }   (payments:write)
GET  /v1/invoices/{id}                    (payments:read)  write-side view
POST /v1/invoices/{id}/capture            Idempotency-Key required   (payments:write)
POST /v1/payments/{id}/refund             Idempotency-Key required   (payments:write)
GET  /v1/payments                         (payments:read)  read model, eventually consistent
```

Events on the `batchyard.events` topic exchange: `payment.captured`,
`payment.refunded`, `plan.changed`.

## Acceptance

- [ ] Hand-written mediator with validation, logging and transaction behaviors (ADR 0010)
- [ ] CreateInvoice, CapturePayment, RefundPayment commands; GetInvoice, ListTenantPayments queries
- [ ] Capture twice with the same key -> one gateway charge, one payment, same response
- [ ] Concurrent captures with different keys on one invoice -> exactly one succeeds (optimistic concurrency), the other gets 409
- [ ] Fake gateway adapter with retries and a timeout (Polly)
- [ ] Outbox: events committed with the state change and published to RabbitMQ with confirms (ADR 0011)
- [ ] Read model updated from payment events; consumer idempotent; eventual consistency documented (ADR 0012)
- [ ] plan.changed published by Access and used to clear the permission cache (ADR 0013)
- [ ] Handler unit tests; integration tests against real SQL Server and RabbitMQ
