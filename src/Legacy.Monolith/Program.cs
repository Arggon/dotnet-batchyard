using BuildingBlocks.Auth;
using BuildingBlocks.Observability;
using Legacy.Monolith;

var builder = WebApplication.CreateBuilder(args);
builder.AddBatchyardTelemetry("legacy-monolith");

// Shares the items database with ingest-svc during the migration; ingest's DbUp scripts own the schema (ADR 0015).
var connectionString = builder.Configuration.GetConnectionString("Items")
    ?? throw new InvalidOperationException("Connection string 'Items' is not configured.");
var maxBatch = builder.Configuration.GetValue("Legacy:MaxBatchSize", 5000);

builder.Services.AddSingleton(new LegacyItemStore(connectionString));
builder.Services.AddBatchyardJwt(builder.Configuration);
builder.Services.AddProblemDetails();

var app = builder.Build();
app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.MapPost("/v1/items", async (LegacyItem item, HttpContext http, LegacyItemStore store) =>
{
    if (LegacyRules.Check(item) is { } error)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]> { ["item"] = [error] });
    }

    var created = await store.SaveAsync(http.User.TenantId(), item, http.RequestAborted);
    return Results.Json(new { sku = item.Sku, outcome = created ? "created" : "updated" },
        statusCode: created ? StatusCodes.Status201Created : StatusCodes.Status200OK);
}).RequirePermission("items:write");

app.MapPost("/v1/items:batch", async (LegacyBatch batch, HttpContext http, LegacyItemStore store) =>
{
    var max = Math.Min(maxBatch, http.User.MaxBatch() ?? maxBatch);
    var count = batch.Items?.Count ?? 0;
    if (count == 0 || count > max)
    {
        return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Invalid batch size",
            detail: $"A batch must contain between 1 and {max} items; this one has {count}.");
    }

    return Results.Ok(await store.SaveBatchAsync(http.User.TenantId(), batch.Items!, http.RequestAborted));
}).RequirePermission("ingest:batch");

app.MapGet("/v1/items/{sku}", async (string sku, HttpContext http, LegacyItemStore store) =>
    await store.FindAsync(http.User.TenantId(), sku, http.RequestAborted) is { } item ? Results.Ok(item) : Results.NotFound())
    .RequirePermission("items:read");

app.MapGet("/v1/reports/stock", async (HttpContext http, LegacyItemStore store) =>
    Results.Ok(await store.StockAsync(http.User.TenantId(), http.RequestAborted)))
    .RequirePermission("items:read");

app.Run();

/// <summary>Entry point, public so integration tests can host the app.</summary>
public partial class Program;
