# ADR 0006: Auth model: the access service issues short-lived JWTs

- **Status:** Proposed
- **Date:** 2026-10-14
- **Deciders:** Gonzalo (product owner); drafted by the maker agent

## Context

Services need to know the caller's tenant and permissions. A real deployment would use an OAuth2/OIDC provider (Cognito, Entra ID, Auth0), but this repo must run offline from one `docker compose up`.

## Decision

The access service is a small OAuth2-style issuer: `POST /v1/auth/token` exchanges API client credentials for a 15-minute JWT signed with HMAC-SHA256. Claims: `sub` (client), `tenant_id`, `plan`, `role`, `perm` and `max_batch`. Every service validates issuer, audience, lifetime and signature with the shared `BuildingBlocks` setup. Endpoints check permissions, never roles.

## Consequences

- Services authorize from the token alone: no call to Access per request.
- A plan change reaches other services when the client next gets a token (up to 15 minutes). Access's own endpoints see it at once (ADR 0008).
- **Mapping to a real provider:** swap the issuer for the provider's client-credentials flow, keep the claim names (or map them in `JwtBearerEvents.OnTokenValidated`), and switch to asymmetric keys from the provider's JWKS endpoint. Endpoint code does not change.
- The HMAC key is a shared secret, acceptable for one trust domain in a demo; asymmetric keys (RS256/JWKS) are the production choice.

## Alternatives considered

- **Embed an OIDC server (OpenIddict, Duende):** realistic, but a lot of surface for a demo; Duende is commercially licensed.
- **Permissions looked up per request from Access:** always fresh, but couples every request to Access availability.
