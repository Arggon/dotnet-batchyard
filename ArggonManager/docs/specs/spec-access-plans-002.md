---
spec_id: access-plans-002
title: "Access: plans, roles and permissions"
status: proposed
created: 2026-10-14
---

# Spec: Access: plans, roles and permissions (access-plans-002)

## Purpose

Tenants buy a plan, and the plan decides what their API clients may do. A
marketplace subscription must be able to grant a new, restricted role without
code changes elsewhere. This is the generic, invented version of "an access role
and permissions that enabled AWS Marketplace monetization".

Invariants:

- **Authorization is data, not code.** Plans grant roles; roles carry
  permissions and restrictions. Endpoints only ask "does the caller have
  permission X?".
- **The tenant always comes from a signed token**, never from a header or body.
- **Least surprise on plan change.** Changing the plan swaps the roles the plan
  granted and keeps roles assigned by hand (tenant-admin).
- **No secrets in the repo.** Client secrets are stored as PBKDF2 hashes; the
  signing key comes from configuration (dev values only in dev config/compose).

## Synopsis

Catalog (seeded as versioned SQL):

| Permission | Meaning |
|---|---|
| `items:read` | read items |
| `items:write` | single-item writes |
| `ingest:batch` | batch writes |
| `exports:read` | read-only exports (placeholder endpoint) |
| `plan:manage` | change the tenant's plan |

| Role | Permissions | Max batch |
|---|---|---|
| `viewer` | items:read | - |
| `writer` | items:read, items:write | - |
| `batch-writer` | items:read, items:write, ingest:batch | 5,000 |
| `marketplace-seller` | items:read, items:write, ingest:batch, exports:read | 1,000 |
| `tenant-admin` | plan:manage | - |

| Plan | Grants roles |
|---|---|
| Free | viewer, writer |
| Pro | viewer, writer, batch-writer |
| Marketplace | viewer, writer, marketplace-seller |

```http
POST /v1/auth/token  { "clientId": "...", "clientSecret": "..." }
-> 200 { "accessToken": "<jwt>", "tokenType": "Bearer", "expiresIn": 900 }
   claims: sub, tenant_id, plan, role[], perm[], max_batch (when allowed)
GET  /v1/tenants/me                 (any valid token) -> tenant, plan, roles, permissions
PUT  /v1/tenants/me/plan { "plan": "Pro" }   (plan:manage) -> 200 new plan
POST /v1/webhooks/marketplace       (X-Webhook-Secret) { "tenantId": "...", "event": "subscribe" | "unsubscribe" }
GET  /v1/exports/items              (exports:read) -> 200 placeholder
```

## Acceptance

- [ ] Plans, roles, permissions and their links are seeded by DbUp scripts in `db/access/`
- [ ] Effective permissions and max batch are resolved from a client's assignments
- [ ] Plan change swaps plan-granted roles, keeps tenant-admin, and invalidates cached permissions
- [ ] `POST /v1/auth/token` issues a 15-minute JWT for valid client credentials; wrong secret -> 401; rate limited per IP (429)
- [ ] Endpoint filter `RequirePermission("...")` returns 401 without a token and 403 without the permission
- [ ] Marketplace webhook stub (shared secret) moves the tenant to the Marketplace plan
- [ ] Authorization matrix tests (role x endpoint -> status) as data-driven xUnit theories, for Access and Ingest
- [ ] Permission lookups are cached in Redis (cache-aside, TTL), proven by tests against real Redis
- [ ] Ingest takes the tenant from the token and enforces items:write / ingest:batch / items:read and `max_batch`
