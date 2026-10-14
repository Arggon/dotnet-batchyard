# Benchmarks

All numbers on this page were measured in this repo. None are copied from the CV.

## Monolith vs extracted service (k6, M4)

The same load against the legacy monolith (one `SELECT` plus one `INSERT` per row) and against ingest-svc (one TVP, one `MERGE`), then against ingest-svc through the YARP gateway. Script: [`benchmarks/k6/strangler.js`](../benchmarks/k6/strangler.js). 5 VUs, 30 s per target, JWT auth on, every row a new insert.

Measured 2026-10-14 on the cloud VM described under Environment. All four apps (access, ingest, legacy, gateway) ran as Release builds on the host; SQL Server 2022 ran in Docker on the same VM. Both backends write the same `dbo.Items` table.

**500 items per request** (two runs, both shown):

| Target | p50 | p95 | Items/s |
|---|---:|---:|---:|
| legacy monolith, run 1 | 316.8 ms | 453.7 ms | 7,450 |
| ingest-svc, run 1 | 21.0 ms | 36.9 ms | 92,517 |
| ingest-svc via gateway, run 1 | 21.9 ms | 31.5 ms | 91,850 |
| legacy monolith, run 2 | 293.2 ms | 376.4 ms | 7,933 |
| ingest-svc, run 2 | 20.0 ms | 30.5 ms | 99,717 |
| ingest-svc via gateway, run 2 | 20.9 ms | 30.7 ms | 98,233 |

**50 items per request** (one run):

| Target | p50 | p95 | Items/s |
|---|---:|---:|---:|
| legacy monolith | 31.8 ms | 45.4 ms | 7,027 |
| ingest-svc | 6.5 ms | 10.6 ms | 32,198 |
| ingest-svc via gateway | 6.7 ms | 12.6 ms | 29,478 |

What this says, and what it doesn't:

- At 500 items per request the service's p50 is **93% lower** than the monolith's in both runs. At 50 items it is **80% lower**. The gap grows with batch size because the monolith pays two round trips per row.
- The gateway added **0.2–0.9 ms at p50** in these runs.
- The speedup comes from the data path (set-based SQL instead of a per-row loop), not from "microservices" as such. Extracting the service is what made it safe to rewrite that path behind the same contract.
- One VM, short runs, the database on the same machine as the apps. Treat the ratios as indicative, not as production numbers.

Reproduce: start the stack (`docker compose up --build`), then `k6 run benchmarks/k6/strangler.js` (defaults: legacy :8083, service :8080, gateway :8000). The `load-test` workflow also runs it on a GitHub runner.

## Re-measured with JWT auth (M2)

Since M2, every request carries a bearer token that Ingest validates. Both tools were re-run on the same VM on 2026-10-14 (API Release builds on the host; SQL Server and Redis in Docker):

| Tool | one-by-one / single | batch | Batch advantage |
|---|---:|---:|---:|
| Demo client, 50,000 items | 87.3 s (573 items/s) | 1.8 s (28,045 items/s) | 98.0% less time |
| k6 single (20 VUs) vs batch (5 VUs × 500) | p50 3.5 ms, p95 6.2 ms, 4,614 items/s | p50 23.1 ms, p95 44.9 ms, 80,917 items/s | ~17.5× items/s |

The single-item path barely moved. Batch throughput came out lower than in the M1 run below (80,917 vs 105,067 items/s on k6, 1.8 s vs 1.3 s for the demo). Some of that is token validation on each request, and some is VM noise; I haven't separated the two. The M1 numbers are kept below as history.

## End to end over HTTP: 50,000 items (demo client, M1, before auth)

`tools/Ingest.DemoClient` imports the same 50,000 items into a running API twice: first with one `POST /v1/items` per item, then with `POST /v1/items:batch` in chunks of 5,000. Each run uses a fresh tenant, so both runs insert 50,000 new rows. Requests are sent one after another (no parallelism), the way a simple client loop would.

Measured 2026-10-14 on the VM described under Environment below. The API ran as a Release build on the host and SQL Server ran in Docker on the same VM:

| Mode | Requests | Wall time | Items/s |
|---|---:|---:|---:|
| one-by-one | 50,000 | 86.3 s | 580 |
| batch (5,000/request) | 10 | 1.3 s | 37,304 |

**Batch saved 98.4% of the wall time.** Client, API and database were on one machine, so there was no real network latency. Over a real network each of the 50,000 single requests would pay a full round trip, which widens the gap.

Reproduce:

```bash
docker compose up --build -d   # seeds the demo client the tools use by default
dotnet run -c Release --project tools/Ingest.DemoClient -- --items 50000 --batch-size 5000
```

## Under concurrent load (k6, M1, before auth)

`benchmarks/k6/ingest.js` runs two scenarios one after the other, 30 s each:

- **single:** 20 virtual users posting one item per request
- **batch:** 5 virtual users posting 500 items per request

Every request inserts new rows. Thresholds fail the run if more than 1% of requests fail or p95 goes over 500 ms (single) or 3 s (batch). The scenarios use different user counts, so compare items/s, not latency.

Measured 2026-10-14 in two places:

| Where | Scenario | p50 | p95 | Items written | Items/s |
|---|---|---:|---:|---:|---:|
| Cloud VM (8 vCPU; API Release build on the host, SQL Server in Docker) | single | 3.4 ms | 6.2 ms | 147,256 | 4,909 |
| | batch (500) | 18.8 ms | 30.4 ms | 3,152,000 | 105,067 |
| GitHub Actions `ubuntu-latest` (4 vCPU; API + SQL Server in `docker compose`) | single | 5.8 ms | 37.5 ms | 46,067 | 1,536 |
| | batch (500) | 36.0 ms | 223.6 ms | 993,500 | 33,117 |

Both runs had 0% failed requests. On both machines, batching wrote about **21× more items per second**. Shared CI runners are noisy, so the CI numbers are for spotting regressions, not for quoting.

Reproduce locally, or run the `load-test` workflow from the Actions tab:

```bash
docker compose up --build -d
k6 run benchmarks/k6/ingest.js            # -e DURATION=60s -e BATCH_SIZE=1000 to tune
```

## Data access: one-by-one vs batch (BenchmarkDotNet)

`benchmarks/Ingest.Benchmarks` inserts N new items for a fresh tenant in two ways:

- **one-by-one:** N calls to `usp_UpsertItem`, one round trip each (the "before")
- **batch:** one call to `usp_UpsertItemsBatch` with a table-valued parameter and a set-based `MERGE` (the "after", ADR 0001)

Measured 2026-10-14:

| Items | one-by-one | batch (TVP + MERGE) | Time saved | Allocated (one-by-one → batch) |
|------:|-----------:|--------------------:|-----------:|-------------------------------:|
| 100   | 163.0 ms   | 7.8 ms              | 95%        | 610 KB → 79 KB                 |
| 1,000 | 1,356.7 ms | 32.7 ms             | 98%        | 5.9 MB → 0.7 MB                |
| 5,000 | 7,551.3 ms | 155.0 ms            | 98%        | 29.4 MB → 3.8 MB               |

Raw BenchmarkDotNet output (mean of 8 iterations, 2 warmups, one invocation per iteration):

```
| Method                | Items | Mean         | Error      | StdDev     | Ratio | Allocated   |
|---------------------- |------ |-------------:|-----------:|-----------:|------:|------------:|
| one-by-one            | 100   |   163.014 ms |  30.602 ms |  13.588 ms |  1.01 |   609.85 KB |
| batch (TVP + MERGE)   | 100   |     7.787 ms |   3.225 ms |   1.150 ms |  0.05 |    78.77 KB |
| one-by-one            | 1000  | 1,356.699 ms | 211.281 ms | 110.504 ms |  1.01 |  6059.58 KB |
| batch (TVP + MERGE)   | 1000  |    32.695 ms |  23.834 ms |  10.582 ms |  0.02 |   682.25 KB |
| one-by-one            | 5000  | 7,551.287 ms | 949.627 ms | 496.673 ms |  1.00 | 30082.07 KB |
| batch (TVP + MERGE)   | 5000  |   155.033 ms |  74.011 ms |  32.861 ms |  0.02 |  3843.69 KB |
```

### Environment

- BenchmarkDotNet 0.15.8, .NET 10.0.13 (SDK 10.0.402), Release build
- Intel Xeon 2.40 GHz, 8 cores, 16 GB RAM, Debian 13, cloud VM
- SQL Server 2022 Developer in Docker on the same VM (vfs storage driver), started by Testcontainers

### How to read it

- The gap is mostly round trips: the one-by-one path pays connection, call and commit overhead per row. Here the database runs on the same machine, so every round trip is cheap. Over a real network the one-by-one path would be slower, so these ratios are on the conservative side for that.
- Docker on a shared cloud VM is noisy (see the Error column). Treat the ratios as the result and the absolute times as rough.
- The 80% on the CV comes from a different system, measured end to end at the client. This page measures only the data-access layer. The k6 comparison against the legacy monolith comes in milestone 4.

### Reproduce

```bash
dotnet run -c Release --project benchmarks/Ingest.Benchmarks -- --filter '*'
```

Docker is required: the benchmark starts its own SQL Server container.
