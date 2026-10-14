# ADR 0009: Tenant comes from the token (supersedes ADR 0004)

- **Status:** Proposed
- **Date:** 2026-10-14
- **Deciders:** Gonzalo (product owner); drafted by the maker agent

## Context

ADR 0004 took the tenant from an `X-Tenant-Id` header until auth existed. M2 adds auth.

## Decision

Ingest requires a valid access token and reads the tenant from the `tenant_id` claim. The header is no longer read. `TenantHeader` is removed from BuildingBlocks.

## Consequences

- A caller can no longer write into another tenant by changing a header.
- Tools (smoke script, demo client, k6) fetch a token first.

## Alternatives considered

- **Keep the header as an override for admins:** an unnecessary way to cross tenants.
