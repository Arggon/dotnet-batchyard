using BenchmarkDotNet.Running;

// Run: dotnet run -c Release --project benchmarks/Ingest.Benchmarks
// Needs Docker (the benchmark starts its own SQL Server container).
BenchmarkSwitcher.FromAssembly(typeof(Ingest.Benchmarks.UpsertBenchmarks).Assembly).Run(args);
