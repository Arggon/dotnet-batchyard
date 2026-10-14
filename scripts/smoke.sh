#!/usr/bin/env bash
# Smoke test against a running stack (docker compose up). Used by CI; handy locally too.
# Uses the demo client seeded by docker-compose.yml (local-only credentials).
set -euo pipefail
BASE_URL="${BASE_URL:-http://localhost:8080}"
ACCESS_URL="${ACCESS_URL:-http://localhost:8081}"
PAYMENTS_URL="${PAYMENTS_URL:-http://localhost:8082}"
LEGACY_URL="${LEGACY_URL:-http://localhost:8083}"
CLIENT_ID="${CLIENT_ID:-demo-admin}"
CLIENT_SECRET="${CLIENT_SECRET:-local-dev-only-client-secret}"

wait_for() {
  for _ in $(seq 1 60); do
    curl -fsS "$1/health" >/dev/null 2>&1 && return 0
    sleep 2
  done
  echo "$1 did not become healthy" >&2
  return 1
}
wait_for "$ACCESS_URL"
wait_for "$BASE_URL"
wait_for "$PAYMENTS_URL"
wait_for "$LEGACY_URL"

token=$(curl -fsS -X POST "$ACCESS_URL/v1/auth/token" -H "Content-Type: application/json" \
  -d "{\"clientId\":\"$CLIENT_ID\",\"clientSecret\":\"$CLIENT_SECRET\"}" | sed -E 's/.*"accessToken":"([^"]+)".*/\1/')
echo "access token issued"

# Without a token, ingest must refuse.
status=$(curl -s -o /dev/null -w '%{http_code}' -X POST "$BASE_URL/v1/items:batch" -H "Content-Type: application/json" -d '{"items":[]}')
[ "$status" = "401" ] || { echo "expected 401 without a token, got $status" >&2; exit 1; }

run=$(date +%s)
response=$(curl -fsS -X POST "$BASE_URL/v1/items:batch" \
  -H "Content-Type: application/json" -H "Authorization: Bearer $token" \
  -d "{\"items\":[
        {\"sku\":\"BRK-$run\",\"name\":\"Brake pad\",\"price\":12.5,\"currency\":\"USD\",\"quantity\":40},
        {\"sku\":\"FLT-$run\",\"name\":\"Oil filter\",\"price\":-1,\"currency\":\"USD\",\"quantity\":3}
      ]}")
echo "$response"
echo "$response" | grep -q '"created":1' && echo "$response" | grep -q '"rejected":1'
echo "ingest ok"

# The legacy monolith writes the same table with the same contract.
legacy=$(curl -fsS -X POST "$LEGACY_URL/v1/items:batch" -H "Content-Type: application/json" -H "Authorization: Bearer $token" \
  -d "{\"items\":[{\"sku\":\"LEG-$run\",\"name\":\"Spark plug\",\"price\":3,\"currency\":\"USD\",\"quantity\":10}]}")
echo "$legacy" | grep -q '"created":1' || { echo "legacy batch failed: $legacy" >&2; exit 1; }
curl -fsS "$BASE_URL/v1/items/LEG-$run" -H "Authorization: Bearer $token" | grep -q "Spark plug" || { echo "ingest cannot read the legacy row" >&2; exit 1; }
echo "legacy ok (row visible through ingest-svc)"

# Payments: create an invoice, capture it twice with one key, then wait for the read model (via RabbitMQ).
invoice=$(curl -fsS -X POST "$PAYMENTS_URL/v1/invoices" -H "Content-Type: application/json" -H "Authorization: Bearer $token" \
  -d "{\"amount\":99.90,\"currency\":\"USD\",\"reference\":\"SMOKE-$run\"}" | sed -E 's/.*"id":"([^"]+)".*/\1/')
first=$(curl -s -o /dev/null -w '%{http_code}' -X POST "$PAYMENTS_URL/v1/invoices/$invoice/capture" \
  -H "Authorization: Bearer $token" -H "Idempotency-Key: smoke-$run")
second=$(curl -s -o /dev/null -w '%{http_code}' -X POST "$PAYMENTS_URL/v1/invoices/$invoice/capture" \
  -H "Authorization: Bearer $token" -H "Idempotency-Key: smoke-$run")
[ "$first" = "201" ] && [ "$second" = "200" ] || { echo "capture: expected 201 then 200, got $first then $second" >&2; exit 1; }
echo "capture is idempotent"
for _ in $(seq 1 30); do
  curl -fsS "$PAYMENTS_URL/v1/payments" -H "Authorization: Bearer $token" | grep -q "$invoice" && { echo "read model updated via RabbitMQ"; break; }
  sleep 1
done
curl -fsS "$PAYMENTS_URL/v1/payments" -H "Authorization: Bearer $token" | grep -q "$invoice" || { echo "payment never reached the read model" >&2; exit 1; }
echo "smoke test passed"
