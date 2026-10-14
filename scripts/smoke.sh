#!/usr/bin/env bash
# Smoke test against a running stack (docker compose up). Used by CI; handy locally too.
set -euo pipefail
BASE_URL="${BASE_URL:-http://localhost:8080}"
TENANT="${TENANT:-$(cat /proc/sys/kernel/random/uuid)}"

for _ in $(seq 1 60); do
  curl -fsS "$BASE_URL/health" >/dev/null 2>&1 && break
  sleep 2
done

response=$(curl -fsS -X POST "$BASE_URL/v1/items:batch" \
  -H "Content-Type: application/json" -H "X-Tenant-Id: $TENANT" \
  -d '{"items":[
        {"sku":"BRK-001","name":"Brake pad","price":12.5,"currency":"USD","quantity":40},
        {"sku":"FLT-002","name":"Oil filter","price":-1,"currency":"USD","quantity":3}
      ]}')
echo "$response"
echo "$response" | grep -q '"created":1' && echo "$response" | grep -q '"rejected":1'
ACCESS_URL="${ACCESS_URL:-http://localhost:8081}"
for _ in $(seq 1 60); do
  curl -fsS "$ACCESS_URL/health" >/dev/null 2>&1 && break
  sleep 2
done
curl -fsS -X POST "$ACCESS_URL/v1/auth/token" -H "Content-Type: application/json" \
  -d '{"clientId":"demo-admin","clientSecret":"local-dev-only-client-secret"}' | grep -q '"accessToken"'
echo "access token issued"
echo "smoke test passed"
