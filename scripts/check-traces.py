#!/usr/bin/env python3
"""Checks that the Aspire dashboard actually received the traces the smoke test produced (ADR 0016).

Run after scripts/smoke.sh against the compose stack. It reads the dashboard's telemetry HTTP API
(/api/telemetry/*, Aspire 13.2+), the same data the dashboard UI shows, and asserts:
  1. every service and the gateway reported traces;
  2. the gateway's batch request and ingest-api's handling of it are one trace (W3C context over HTTP);
  3. a payment capture is one trace from the HTTP request through `payment.captured publish` to
     `payment.captured process` in the read-model consumer (context carried through the outbox and RabbitMQ).
Standard library only, so CI needs nothing extra.
"""
import json
import os
import sys
import time
import urllib.request

BASE = os.environ.get("DASHBOARD_URL", f"http://localhost:{os.environ.get('DASHBOARD_PORT', '18888')}")
SERVICES = ["gateway", "ingest-api", "access-api", "payments-api", "legacy-monolith"]
DEADLINE = time.time() + float(os.environ.get("TRACE_WAIT_SECONDS", "60"))


def get(path):
    with urllib.request.urlopen(BASE + path, timeout=10) as response:
        return json.load(response)


def spans(payload):
    """Flattens OTLP ResourceSpans JSON into (service, span) pairs."""
    for rs in payload.get("data", {}).get("resourceSpans", []):
        attrs = {a["key"]: a.get("value", {}).get("stringValue") for a in rs.get("resource", {}).get("attributes", [])}
        for ss in rs.get("scopeSpans", []):
            for span in ss.get("spans", []):
                yield attrs.get("service.name"), span


def check_services():
    reported = {r["name"] for r in get("/api/telemetry/resources") if r.get("hasTraces")}
    missing = [s for s in SERVICES if s not in reported]
    return not missing, f"services with traces: {sorted(reported)}; missing: {missing}"


def traces_by_id(resource):
    by_id = {}
    for service, span in spans(get(f"/api/telemetry/traces?resource={resource}")):
        by_id.setdefault(span["traceId"], []).append((service, span))
    return by_id


def check_gateway_to_ingest():
    for trace_id, items in traces_by_id("gateway").items():
        services = {s for s, _ in items}
        if {"gateway", "ingest-api"} <= services:
            return True, f"gateway -> ingest-api in one trace ({trace_id}, {len(items)} spans)"
    return False, "no trace contains both gateway and ingest-api spans"


def check_capture_through_rabbitmq():
    for trace_id, items in traces_by_id("payments-api").items():
        names = {span["name"] for _, span in items}
        has_server = any(span.get("kind") == 2 for _, span in items)
        if has_server and {"payment.captured publish", "payment.captured process"} <= names:
            return True, f"capture request -> publish -> process in one trace ({trace_id}, {len(items)} spans)"
    return False, "no payments-api trace holds the HTTP request plus payment.captured publish and process"


def main():
    checks = [check_services, check_gateway_to_ingest, check_capture_through_rabbitmq]
    for check in checks:
        while True:
            try:
                ok, detail = check()
            except OSError as error:  # dashboard not answering yet
                ok, detail = False, f"dashboard API error: {error}"
            if ok or time.time() > DEADLINE:
                break
            time.sleep(2)  # the SDK exports in batches, so give it a moment
        print(("ok   " if ok else "FAIL ") + detail)
        if not ok:
            return 1
    print("dashboard shows the traces")
    return 0


if __name__ == "__main__":
    sys.exit(main())
