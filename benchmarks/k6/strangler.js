// Monolith vs extracted service under the same load (spec strangler-004, ADR 0015).
// Three scenarios, one after another, same batch size and VUs:
//   legacy   - POST /v1/items:batch on the legacy monolith (per-row SELECT + INSERT)
//   service  - POST /v1/items:batch on ingest-svc (one TVP, one MERGE)
//   gateway  - the same service call through the YARP gateway (shows the extra hop)
//
//   k6 run benchmarks/k6/strangler.js
//   k6 run -e DURATION=60s -e BATCH_SIZE=1000 benchmarks/k6/strangler.js
import http from 'k6/http';
import { check } from 'k6';
import { Counter } from 'k6/metrics';
import exec from 'k6/execution';

const LEGACY_URL = __ENV.LEGACY_URL || `http://localhost:${__ENV.LEGACY_PORT || '8083'}`;
const SERVICE_URL = __ENV.SERVICE_URL || `http://localhost:${__ENV.INGEST_PORT || '8080'}`;
const GATEWAY_URL = __ENV.GATEWAY_URL || `http://localhost:${__ENV.GATEWAY_PORT || '8000'}`;
const ACCESS_URL = __ENV.ACCESS_URL || `http://localhost:${__ENV.ACCESS_PORT || '8081'}`;
const CLIENT_ID = __ENV.CLIENT_ID || 'demo-admin';
const CLIENT_SECRET = __ENV.CLIENT_SECRET || 'local-dev-only-client-secret';
const DURATION = __ENV.DURATION || '30s';
const BATCH_SIZE = parseInt(__ENV.BATCH_SIZE || '500', 10);
const VUS = parseInt(__ENV.VUS || '5', 10);
const seconds = parseInt(DURATION, 10) || 30;

const written = {
  legacy: new Counter('items_legacy'),
  service: new Counter('items_service'),
  gateway: new Counter('items_gateway'),
};

const scenario = (name, offset) => ({
  executor: 'constant-vus', exec: name, vus: VUS, duration: DURATION, startTime: `${offset * (seconds + 5)}s`,
});

export const options = {
  scenarios: { legacy: scenario('legacy', 0), service: scenario('service', 1), gateway: scenario('gateway', 2) },
  thresholds: {
    http_req_failed: ['rate<0.01'],
    // Listed so the summary has per-scenario percentiles; generous limits, this is a comparison, not an SLO.
    'http_req_duration{scenario:legacy}': ['p(95)<60000'],
    'http_req_duration{scenario:service}': ['p(95)<60000'],
    'http_req_duration{scenario:gateway}': ['p(95)<60000'],
  },
  summaryTrendStats: ['avg', 'med', 'p(95)', 'max'],
};

export function setup() {
  const res = http.post(`${ACCESS_URL}/v1/auth/token`, JSON.stringify({ clientId: CLIENT_ID, clientSecret: CLIENT_SECRET }),
    { headers: { 'Content-Type': 'application/json' } });
  if (res.status !== 200) throw new Error(`token request failed: ${res.status}`);
  return { token: res.json('accessToken') };
}

const runId = Date.now().toString(36);

function send(name, baseUrl, data) {
  const prefix = `${runId}-${name}${exec.vu.idInTest}-${exec.vu.iterationInScenario}`;
  const items = Array.from({ length: BATCH_SIZE }, (_, i) => (
    { sku: `ST-${prefix}-${i}`, name: `Strangler part ${i}`, price: 4.25, currency: 'USD', quantity: 3 }));
  const res = http.post(`${baseUrl}/v1/items:batch`, JSON.stringify({ items }), {
    headers: { 'Content-Type': 'application/json', Authorization: `Bearer ${data.token}` }, timeout: '120s',
  });
  if (check(res, { [`${name}: all created`]: (r) => r.status === 200 && r.json('created') === BATCH_SIZE })) {
    written[name].add(BATCH_SIZE);
  }
}

export const legacy = (data) => send('legacy', LEGACY_URL, data);
export const service = (data) => send('service', SERVICE_URL, data);
export const gateway = (data) => send('gateway', GATEWAY_URL, data);

export function handleSummary(data) {
  const m = data.metrics;
  const d = (s) => (m[`http_req_duration{scenario:${s}}`] || { values: {} }).values;
  const count = (s) => (m[`items_${s}`] ? m[`items_${s}`].values.count : 0);
  const row = (label, s) => `| ${label} | ${fmt(d(s).med)} | ${fmt(d(s)['p(95)'])} | ${thousands(count(s))} | ${thousands(Math.round(count(s) / seconds))} |`;
  const p50 = (s) => d(s).med;
  const lines = [
    `### k6: monolith vs service (${VUS} VUs, ${DURATION} each, ${BATCH_SIZE} items per request)`,
    '',
    '| Target | p50 | p95 | Items written | Items/s |',
    '|---|---:|---:|---:|---:|',
    row('legacy monolith (per-row SQL)', 'legacy'),
    row('ingest-svc (TVP + MERGE)', 'service'),
    row('ingest-svc through the gateway', 'gateway'),
    '',
    p50('legacy') && p50('service')
      ? `p50 latency: service is ${(100 * (1 - p50('service') / p50('legacy'))).toFixed(0)}% lower than legacy. Gateway vs direct at p50: ${(p50('gateway') - p50('service')).toFixed(1)} ms (a negative value means the hop is below run-to-run noise).`
      : '',
    `Failed requests: ${(m.http_req_failed.values.rate * 100).toFixed(2)}%`,
    '',
  ].join('\n');
  return { stdout: lines + '\n', 'k6-strangler-summary.md': lines };
}

function fmt(ms) {
  return ms === undefined ? 'n/a' : `${ms.toFixed(1)} ms`;
}

function thousands(n) {
  return String(n).replace(/\B(?=(\d{3})+(?!\d))/g, ',');
}
