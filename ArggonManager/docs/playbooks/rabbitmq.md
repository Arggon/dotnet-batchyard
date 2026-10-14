---
playbook_id: rabbitmq
version: 4.1
researched: 2026-10-14
status: current
---

# RabbitMQ playbook (rabbitmq)

Technology playbook: the chosen version and the current best practices for
rabbitmq. The version/best-practices research happened when this file was
created — cite dated sources (URL + access date) in every section so the next
reader can re-verify, and keep this file current via
`arggon playbook status`.

## Setup

- `rabbitmq:4.1-management-alpine` in compose and Testcontainers (`Testcontainers.RabbitMq`). Source: https://www.rabbitmq.com/release-information (accessed 2026-10-14).
- Client: `RabbitMQ.Client` 7.x (async API, Apache-2.0/MPL-2.0).

## Conventions

- One durable topic exchange `batchyard.events`; routing key = event type (`payment.captured`).
- One durable queue per consumer purpose (`payments.read-model`, `access.cache`).
- Persistent messages, publisher confirms, manual acks; nack + requeue on handler failure.
- Messages carry `message_id` = outbox row id, `type`, JSON body.

## Testing

- Real broker via Testcontainers; tests poll for the effect (read model row, cache cleared) with a timeout instead of sleeping.

## Security

- Default `guest` credentials only inside compose/Testcontainers. Production: per-service users, TLS, vhost per environment.

## Upgrade policy

- Re-research on stale status; refresh with `arggon playbook refresh rabbitmq --version <v>`.
