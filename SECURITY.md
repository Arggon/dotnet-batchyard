<!-- arggon:generated template="SECURITY.md" -->
# Security

dotnet-batchyard is a showcase, not a deployed product. There are no supported releases, but security reports are welcome.

## Reporting a vulnerability

Please don't open a public issue. Contact the maintainer, [@Arggon](https://github.com/Arggon), privately through his GitHub profile. Include the commit, steps to reproduce and the impact.

## What the code does about security

| Area | How |
|---|---|
| Authentication | Access issues 15-minute JWTs (HS256) from API client credentials. Every service validates issuer, audience, lifetime, algorithm and signature (ADR 0006) |
| Authorization | Endpoints require permissions, not roles (`RequirePermission`). Without a token → 401, without the permission → 403. Matrix tests cover role × endpoint |
| Tenant isolation | The tenant comes only from the signed token (ADR 0009). Every query filters by tenant |
| Client secrets | Stored as PBKDF2-SHA256 hashes (100k iterations, random salt) and compared in fixed time. Unknown client and wrong secret return the same 401 |
| Brute force | `POST /v1/auth/token` is rate limited per IP (10/min by default, 429 after that) |
| SQL injection | Parameters and TVPs only (Dapper, EF Core LINQ). No SQL is built from input; a test sends a SQL-shaped SKU |
| Input validation | Allow-list rules for SKU, currency and lengths; numeric bounds; batch size caps |
| Webhooks | The marketplace stub needs a shared secret, compared in fixed time. It's disabled when no secret is configured |
| Secrets in the repo | None for real use. `docker-compose.yml` and `appsettings.Development.json` contain clearly labeled **local-only** values for throwaway containers |
| Supply chain | Central package versions, NuGet audit warnings fail the build, CodeQL on every PR |

## Known limitations (by design for a demo)

- HS256 with a shared key. Production would use asymmetric keys from an identity provider's JWKS endpoint.
- Other services see a plan change only when the client gets a new token (up to 15 minutes). The Access service itself applies it immediately.
- No HTTPS inside `docker compose`. TLS would end at a load balancer or gateway.
