using System.Diagnostics;
using System.Globalization;
using System.Net.Http.Json;

// Imports N items twice against a running ingest API: once with one request per item, once with batch
// requests. Each run uses its own SKU prefix, so both runs insert N new items.
// Gets a token from the access service first (defaults: the demo client seeded by docker compose).
//
//   dotnet run -c Release --project tools/Ingest.DemoClient -- [--url http://localhost:8080] [--access-url http://localhost:8081]
//       [--client-id demo-admin] [--client-secret ...] [--items 50000] [--batch-size 5000]
var url = Arg("--url", "http://localhost:8080");
var total = int.Parse(Arg("--items", "50000"), CultureInfo.InvariantCulture);
var batchSize = int.Parse(Arg("--batch-size", "5000"), CultureInfo.InvariantCulture);
var accessUrl = Arg("--access-url", "http://localhost:8081");
var clientId = Arg("--client-id", "demo-admin");
var clientSecret = Arg("--client-secret", "local-dev-only-client-secret");

using var http = new HttpClient { BaseAddress = new Uri(url), Timeout = TimeSpan.FromMinutes(5) };
using (var access = new HttpClient { BaseAddress = new Uri(accessUrl) })
{
    using var tokenResponse = await access.PostAsJsonAsync("/v1/auth/token", new { clientId, clientSecret });
    tokenResponse.EnsureSuccessStatusCode();
    var token = await tokenResponse.Content.ReadFromJsonAsync<TokenResponse>();
    http.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token!.AccessToken);
}

var runId = DateTime.UtcNow.ToString("HHmmss", CultureInfo.InvariantCulture);
object[] Items(string mode) => [.. Enumerable.Range(0, total)
    .Select(i => new { sku = $"{mode}-{runId}-{i:D6}", name = $"Demo part {i}", price = 10m + (i % 1000), currency = "USD", quantity = i % 50 })];

Console.WriteLine($"Importing {total:N0} items into {url}");

var single = await Run("one-by-one", async () =>
{
    var requests = 0;
    foreach (var item in Items("S"))
    {
        using var response = await http.PostAsJsonAsync("/v1/items", item);
        response.EnsureSuccessStatusCode();
        requests++;
    }

    return requests;
});

var batch = await Run($"batch ({batchSize:N0}/request)", async () =>
{
    var requests = 0;
    foreach (var chunk in Items("B").Chunk(batchSize))
    {
        using var response = await http.PostAsJsonAsync("/v1/items:batch", new { items = chunk });
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

async Task<Result> Run(string mode, Func<Task<int>> import)
{
    Console.Write($"  {mode}... ");
    var stopwatch = Stopwatch.StartNew();
    var requests = await import();
    stopwatch.Stop();
    Console.WriteLine($"{stopwatch.Elapsed.TotalSeconds:N1} s");
    return new Result(mode, requests, stopwatch.Elapsed);
}

string Arg(string name, string fallback)
{
    var index = Array.IndexOf(args, name);
    return index >= 0 && index + 1 < args.Length ? args[index + 1] : fallback;
}

internal sealed record Result(string Mode, int Requests, TimeSpan Elapsed);

internal sealed record TokenResponse(string AccessToken);
