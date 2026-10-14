# ADR 0005: Idempotency keys stored in SQL Server

- **Status:** Proposed
- **Date:** 2026-10-14
- **Deciders:** Gonzalo (product owner); drafted by the maker agent

## Context

Clients retry batch uploads after timeouts. A replay must not produce a different answer or double work.

## Decision

`Idempotency-Key` is optional. When present, the API stores the request body hash and the response per (tenant, key) in `dbo.IdempotencyKeys` (primary key on both). Same key + same body replays the stored response; same key + different body returns 409.

## Consequences

- Upserts are already naturally idempotent on data, so the store protects the *response contract* (outcomes stay `created` on replay).
- The key is stored after the upsert commits, not in the same transaction. Two concurrent first requests with one key can both run; the second insert loses and the data result is identical. Accepted for M1; Payments (M3) needs the stricter version and will get it.

## Alternatives considered

- **Redis with TTL:** another moving part in M1.
- **In-memory cache:** lost on restart, wrong with more than one instance.
