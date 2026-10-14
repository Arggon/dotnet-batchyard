# ADR 0003: docker compose as the primary local run path

- **Status:** Proposed
- **Date:** 2026-10-14
- **Deciders:** Gonzalo (product owner); drafted by the maker agent

## Context

The spec left open whether `docker compose` or a .NET Aspire AppHost is the one-command run. Reviewers may not have the .NET SDK installed; most have Docker.

## Decision

`docker compose up` is the primary, documented run path and is smoke-tested in CI. An Aspire AppHost can be added later (M4, with OpenTelemetry) as an optional developer path.

## Consequences

- Works on any machine with Docker, no SDK needed.
- The Aspire dashboard arrives with the observability work instead of on day one.

## Alternatives considered

- **Aspire AppHost first:** attractive to .NET reviewers, but requires the SDK and workload to run, and adds moving parts before there is anything to observe.
