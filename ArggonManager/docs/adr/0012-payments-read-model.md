# ADR 0012: Payments list served from a read model

- **Status:** Proposed
- **Date:** 2026-10-14
- **Deciders:** Gonzalo (product owner); drafted by the maker agent

## Context

`ListTenantPayments` is the read most callers use. The write tables are shaped for consistency (invoices, payments, concurrency tokens), not for listing.

## Decision

A `TenantPayments` table, updated by a consumer of `payment.captured` and `payment.refunded`, serves `GET /v1/payments`. `GET /v1/invoices/{id}` still reads the write side, so a client can always confirm the state of one invoice right away.

## Consequences

- **Eventually consistent:** a capture shows up in the list after the outbox poll and the consumer (usually under 2 s locally). Documented in the API description.
- The read model can be rebuilt by replaying events.

## Alternatives considered

- **Query the write tables with joins:** simpler, but doesn't show CQRS, which is the CV claim.
