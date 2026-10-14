# ADR 0014: YARP gateway with per-route switching

- **Status:** Proposed
- **Date:** 2026-10-14
- **Deciders:** Gonzalo (product owner); drafted by the maker agent

## Context

Moving ingest out of the monolith should happen route by route, reversibly, without clients changing URLs.

## Decision

A YARP gateway in front of everything. Routes and clusters live in configuration; each item route's `ClusterId` is either `legacy` or `ingest`. Flipping it (appsettings or an environment variable such as `ReverseProxy__Routes__items-batch__ClusterId=legacy`) moves the route. Every response carries `X-Backend: <cluster>` so tests and people can see who answered. The gateway does not validate tokens; it forwards the Authorization header and each service validates it, as before.

## Consequences

- One extra network hop (measured in the k6 comparison).
- Switching needs a restart or config reload; no percentage canaries.

## Alternatives considered

- **Nginx/Envoy:** fine, but YARP keeps the gateway in .NET, testable with WebApplicationFactory.
- **Feature flag inside the monolith calling the service:** keeps the monolith in the request path.
