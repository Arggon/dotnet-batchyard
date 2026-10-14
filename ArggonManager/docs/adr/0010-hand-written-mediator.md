# ADR 0010: A hand-written mediator instead of MediatR

- **Status:** Proposed
- **Date:** 2026-10-14
- **Deciders:** Gonzalo (product owner); drafted by the maker agent

## Context

The payments service splits commands and queries and runs every command through the same pipeline (validation, logging, transaction). MediatR is the usual library, but versions from 13 (2025) are commercially licensed.

## Decision

Write a minimal mediator in `Payments.Application` (about 60 lines): `IRequest<T>`, `IRequestHandler<,>`, `IPipelineBehavior<,>`, resolved from DI. Commands are marked with `ICommand<T>` so only they get the transaction behavior.

## Consequences

- No license risk, nothing hidden: a reviewer can read the whole pattern in one file.
- One reflection step per request type to find the handler, cached. Fine at this scale.
- No notifications/streaming: not needed here.

## Alternatives considered

- **MediatR 12 (last free version):** frozen on an old release.
- **martinothamar/Mediator (MIT, source-generated):** faster and still free; a good choice for production, but it hides the pattern this repo wants to show.
