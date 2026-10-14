# ADR 0013: plan.changed drives permission cache invalidation (amends ADR 0008)

- **Status:** Proposed
- **Date:** 2026-10-14
- **Deciders:** Gonzalo (product owner); drafted by the maker agent

## Context

ADR 0008 invalidated the permission cache directly after a plan change. With an outbox and RabbitMQ in place, other parts of the system may also need to react to plan changes.

## Decision

Changing a plan writes `plan.changed` to the access outbox in the same transaction. Access consumes its own event and bumps the tenant's cache generation. The direct call is removed, so there is one path for every reaction to a plan change.

## Consequences

- Invalidation now lags by the outbox poll and delivery (about a second locally).
- If RabbitMQ is down, cached permissions can be stale for up to the 10-minute TTL. Accepted: the change itself is committed, and tokens already lag by up to 15 minutes in other services (ADR 0006).

## Alternatives considered

- **Keep the direct call as well:** faster, but two code paths for one rule.
