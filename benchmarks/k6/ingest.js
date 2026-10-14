// End-to-end load test for the ingest API: concurrent single-item writes, then concurrent batch writes.
//
//   k6 run benchmarks/k6/ingest.js                       # against http://localhost:8080
//   k6 run -e BASE_URL=http://host:8080 -e DURATION=60s benchmarks/k6/ingest.js
//
// Each virtual user writes under its own tenant with unique SKUs, so every request inserts new rows.
import http from 'k6/http';
import { check } from 'k6';
import { Counter } from 'k6/metrics';
import exec from 'k6/execution';

const BASE_URL = __ENV.BASE_URL || 'http://localhost:8080';
const DURATION = __ENV.DURATION || '30s';
const BATCH_SIZE = parseInt(__ENV.BATCH_SIZE || '500', 10);
const SINGLE_VUS = parseInt(__ENV.SINGLE_VUS || '20', 10);
const BATCH_VUS = parseInt(__ENV.BATCH_VUS || '5', 10);

const itemsSingle = new Counter('items_written_single');
const itemsBatch = new Counter('items_written_batch');

export const options = {
  scenarios: {
    single: { executor: 'constant-vus', exec: 'single', vus: SINGLE_VUS, duration: DURATION },
    batch: {
      executor: 'constant-vus', exec: 'batch', vus: BATCH_VUS, duration: DURATION,
      startTime: addSeconds(DURATION, 5),
    },
  },
  thresholds: {
    http_req_failed: ['rate<0.01'],
    'http_req_duration{scenario:single}': ['p(95)<500'],
    'http_req_duration{scenario:batch}': ['p(95)<3000'],
  },
  summaryTrendStats: ['avg', 'med', 'p(95)', 'max'],
};

function addSeconds(duration, extra) {
  const match = /^(\d+)s$/.exec(duration);
  return match ? `${parseInt(match[1], 10) + extra}s` : duration;
}

function tenantFor(scenario) {
  // A stable, valid GUID per VU and scenario.
  const id = exec.vu.idInTest.toString(16).padStart(8, '0');
  return `${id}-0000-4000-8000-${scenario === 'single' ? '00000000000a' : '00000000000b'}`;
}

function item(n) {
  return { sku: `K6-${n}`, name: `Load part ${n}`, price: 9.99, currency: 'USD', quantity: 1 };
}

const headers = (scenario) => ({ 'Content-Type': 'application/json', 'X-Tenant-Id': tenantFor(scenario) });
const runId = Date.now().toString(36);

export function single() {
  const n = `${runId}-${exec.vu.iterationInScenario}`;
  const res = http.post(`${BASE_URL}/v1/items`, JSON.stringify(item(n)), { headers: headers('single') });
  if (check(res, { 'single: 201 created': (r) => r.status === 201 })) itemsSingle.add(1);
}

export function batch() {
  const prefix = `${runId}-${exec.vu.iterationInScenario}`;
  const items = Array.from({ length: BATCH_SIZE }, (_, i) => item(`${prefix}-${i}`));
  const res = http.post(`${BASE_URL}/v1/items:batch`, JSON.stringify({ items }), { headers: headers('batch'), timeout: '60s' });
  if (check(res, { 'batch: all created': (r) => r.status === 200 && r.json('created') === BATCH_SIZE })) {
    itemsBatch.add(BATCH_SIZE);
  }
}

export function handleSummary(data) {
  const m = data.metrics;
  const seconds = parseInt(DURATION, 10) || 30;
  const trend = (name) => m[name] ? m[name].values : null;
  const row = (label, durations, items, vus) => {
    const d = durations || {};
    const count = items ? items.values.count : 0;
    return `| ${label} | ${vus} | ${fmt(d.med)} | ${fmt(d['p(95)'])} | ${thousands(count)} | ${thousands(Math.round(count / seconds))} |`;
  };
  const table = [
    `### k6: ingest API (${DURATION} per scenario, batch size ${BATCH_SIZE})`,
    '',
    '| Scenario | VUs | p50 latency | p95 latency | Items written | Items/s |',
    '|---|---:|---:|---:|---:|---:|',
    row('single `POST /v1/items`', trend('http_req_duration{scenario:single}'), m.items_written_single, SINGLE_VUS),
    row(`batch \`POST /v1/items:batch\``, trend('http_req_duration{scenario:batch}'), m.items_written_batch, BATCH_VUS),
    '',
    `Failed requests: ${(m.http_req_failed.values.rate * 100).toFixed(2)}%`,
    '',
  ].join('\n');
  return { stdout: table + '\n', 'k6-summary.md': table };
}

function fmt(ms) {
  return ms === undefined ? 'n/a' : `${ms.toFixed(1)} ms`;
}

function thousands(n) {
  return String(n).replace(/\B(?=(\d{3})+(?!\d))/g, ',');
}
