# ADR 0004: Tenant taken from the X-Tenant-Id header until M2

- **Status:** Proposed
- **Date:** 2026-10-14
- **Deciders:** Gonzalo (product owner); drafted by the maker agent

## Context

Ingest is tenant-scoped, but authentication (JWT, plans, permissions) is milestone M2.

## Decision

In M1 the tenant comes from a required `X-Tenant-Id` GUID header. In M2 it moves to a JWT claim and the header is removed; the endpoint code reads the tenant through one accessor so the change is local.

## Consequences

- M1 endpoints are not authenticated. That is stated in the README and in `SECURITY.md`; nothing is deployed.

## Alternatives considered

- **Build auth first:** delays the centerpiece milestone by a week.
