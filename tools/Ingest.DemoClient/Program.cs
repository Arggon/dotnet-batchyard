using System.Diagnostics;
using System.Globalization;
using System.Net.Http.Json;

// Imports the same N items twice against a running ingest API: once with one request per item,
// once with batch requests. Each run uses a fresh tenant, so both runs insert N new items.
//
//   dotnet run -c Release --project tools/Ingest.DemoClient -- [--url http://localhost:8080] [--items 50000] [--batch-size 5000]
var url = Arg("--url", "http://localhost:8080");
var total = int.Parse(Arg("--items", "50000"), CultureInfo.InvariantCulture);
var batchSize = int.Parse(Arg("--batch-size", "5000"), CultureInfo.InvariantCulture);

using var http = new HttpClient { BaseAddress = new Uri(url), Timeout = TimeSpan.FromMinutes(5) };
var items = Enumerable.Range(0, total)
    .Select(i => new { sku = $"DEMO-{i:D6}", name = $"Demo part {i}", price = 10m + (i % 1000), currency = "USD", quantity = i % 50 })
    .ToArray();

Console.WriteLine($"Importing {total:N0} items into {url}");

var single = await Run("one-by-one", async tenant =>
{
    var requests = 0;
    foreach (var item in items)
    {
        using var response = await http.SendAsync(Post("/v1/items", item, tenant));
        response.EnsureSuccessStatusCode();
        requests++;
    }

    return requests;
});

var batch = await Run($"batch ({batchSize:N0}/request)", async tenant =>
{
    var requests = 0;
    foreach (var chunk in items.Chunk(batchSize))
    {
        using var response = await http.SendAsync(Post("/v1/items:batch", new { items = chunk }, tenant));
        response.EnsureSuccessStatusCode();
        requests++;
    }

    return requests;
});

Console.WriteLine();
Console.WriteLine("| Mode | Requests | Wall time | Items/s |");
Console.WriteLine("|---|---:|---:|---:|");
foreach (var r in new[] { single, batch })
{
    Console.WriteLine($"| {r.Mode} | {r.Requests:N0} | {r.Elapsed.TotalSeconds:N1} s | {total / r.Elapsed.TotalSeconds:N0} |");
}

Console.WriteLine();
Console.WriteLine($"Batch saved {1 - (batch.Elapsed / single.Elapsed):P1} of the wall time.");

async Task<Result> Run(string mode, Func<Guid, Task<int>> import)
{
    var tenant = Guid.NewGuid();
    Console.Write($"  {mode}... ");
    var stopwatch = Stopwatch.StartNew();
    var requests = await import(tenant);
    stopwatch.Stop();
    Console.WriteLine($"{stopwatch.Elapsed.TotalSeconds:N1} s");
    return new Result(mode, requests, stopwatch.Elapsed);
}

static HttpRequestMessage Post(string path, object body, Guid tenant)
{
    var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) };
    request.Headers.Add("X-Tenant-Id", tenant.ToString());
    return request;
}

string Arg(string name, string fallback)
{
    var index = Array.IndexOf(args, name);
    return index >= 0 && index + 1 < args.Length ? args[index + 1] : fallback;
}

internal sealed record Result(string Mode, int Requests, TimeSpan Elapsed);
