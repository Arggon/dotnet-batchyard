---
type: task
status: done
id: task-m4-dashboard-ci
title: CI checks the Aspire dashboard received the traces
assignee: Arggon
parent: m4-observability
labels: []
created: "2026-10-14"
updated: "2026-10-14"
---
<!--
  Placement (v0): ArggonManager/showcase-v1/m4-strangler/m4-observability/task-m4-dashboard-ci.md
  Leaves live only under a story. id is the filename stem: task-m4-dashboard-ci.
  CLI `arggon create task m4-dashboard-ci` adds the task- prefix (do not pass it twice).
  parent MUST be the story id. Omit assignee when unassigned. Omit blocked_reason unless status is blocked.
-->

# CI checks the Aspire dashboard received the traces

## Context

Open note on m4-observability: the dashboard view was not checked in CI (the smoke test only checked that the dashboard was up). Gonzalo asked for a real check before acceptance.

## Acceptance

- [x] After the compose smoke test, CI queries the dashboard's telemetry API (`scripts/check-traces.py`)
- [x] Fails unless every service and the gateway reported traces
- [x] Fails unless gateway -> ingest-api is one trace and a capture is one trace through payment.captured publish/process (RabbitMQ)
- [x] Negative control: with the dashboard's data cleared, the check fails
- [x] README and ADR 0016 updated

## Notes

### 2026-10-14 @Arggon
Evidence (this PR): local compose run: smoke passed, check-traces.py ok on all three checks (gateway->ingest trace 6 spans, capture trace 10 spans); after restarting the dashboard (data cleared) it fails with missing gateway/legacy-monolith. CI step 'Dashboard shows the traces' runs in the compose-smoke job. Dashboard image pinned to 13.6.1 (13.0 has no telemetry API).
