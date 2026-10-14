# Benchmarks

All numbers on this page were measured in this repo. None are copied from the CV.

## End to end over HTTP: 50,000 items (demo client)

`tools/Ingest.DemoClient` imports the same 50,000 items into a running API twice: first with one `POST /v1/items` per item, then with `POST /v1/items:batch` in chunks of 5,000. Each run uses a fresh tenant, so both runs insert 50,000 new rows. Requests are sent one after another (no parallelism), the way a simple client loop would.

Measured 2026-10-14 on the VM described under Environment below. The API ran as a Release build on the host and SQL Server ran in Docker on the same VM:

| Mode | Requests | Wall time | Items/s |
|---|---:|---:|---:|
| one-by-one | 50,000 | 86.3 s | 580 |
| batch (5,000/request) | 10 | 1.3 s | 37,304 |

**Batch saved 98.4% of the wall time.** Client, API and database were on one machine, so there was no real network latency. Over a real network each of the 50,000 single requests would pay a full round trip, which widens the gap.

Reproduce:

```bash
docker compose up --build -d
dotnet run -c Release --project tools/Ingest.DemoClient -- --items 50000 --batch-size 5000
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
- The 80% on the CV comes from a different system, measured end to end at the client. This page measures only the data-access layer. The end-to-end k6 comparison comes in milestone 4.

### Reproduce

```bash
dotnet run -c Release --project benchmarks/Ingest.Benchmarks -- --filter '*'
```

Docker is required: the benchmark starts its own SQL Server container.
