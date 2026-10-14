using BuildingBlocks.Auth;
using Ingest.Data;
using Microsoft.Extensions.Options;

namespace Ingest.Api.Items;

public static class ItemEndpoints
{
    public static IEndpointRouteBuilder MapItemEndpoints(this IEndpointRouteBuilder app)
    {
        var items = app.MapGroup("/v1/items").WithTags("Items");

        items.MapPost("", UpsertOne)
            .RequirePermission("items:write")
            .WithSummary("Create or update one item (one database round trip per item).");
        // Google AIP-136 style custom method. Mapped outside the group so the route is /v1/items:batch, not /v1/items/:batch.
        app.MapPost("/v1/items:batch", UpsertBatch)
            .RequirePermission("ingest:batch")
            .WithTags("Items")
            .WithSummary("Create or update up to MaxBatchSize items in one call (TVP + MERGE).");
        items.MapGet("{sku}", GetOne)
            .RequirePermission("items:read")
            .WithSummary("Read one item.");

        return app;
    }

    private static async Task<IResult> UpsertOne(
        ItemRequest request, HttpContext http, IItemRepository repository, IIdempotencyStore idempotency)
    {
        var tenantId = http.User.TenantId();

        var (row, error) = ItemValidator.Validate(request);
        if (row is null)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["item"] = [error!] });
        }

        return await Idempotency.RunAsync(http, idempotency, tenantId, request, async () =>
        {
            var outcome = await repository.UpsertAsync(tenantId, row, http.RequestAborted);
            return outcome == UpsertOutcome.Created
                ? (StatusCodes.Status201Created, new ItemResponse(row.Sku, ItemOutcome.Created))
                : (StatusCodes.Status200OK, new ItemResponse(row.Sku, ItemOutcome.Updated));
        });
    }

    private static async Task<IResult> UpsertBatch(
        BatchRequest request,
        HttpContext http,
        IItemRepository repository,
        IIdempotencyStore idempotency,
        IOptions<IngestOptions> options)
    {
        var tenantId = http.User.TenantId();

        // The smaller of the service limit and the caller's plan limit (max_batch claim).
        var max = Math.Min(options.Value.MaxBatchSize, http.User.MaxBatch() ?? options.Value.MaxBatchSize);
        var count = request.Items?.Count ?? 0;
        if (count == 0 || count > max)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid batch size",
                detail: $"A batch must contain between 1 and {max} items; this one has {count}.");
        }

        return await Idempotency.RunAsync(http, idempotency, tenantId, request, async () =>
        {
            var response = await ProcessBatch(tenantId, request.Items!, repository, http.RequestAborted);
            return (StatusCodes.Status200OK, response);
        });
    }

    private static async Task<BatchResponse> ProcessBatch(
        Guid tenantId, IReadOnlyList<ItemRequest> items, IItemRepository repository, CancellationToken cancellationToken)
    {
        var results = new BatchItemResult[items.Count];
        var valid = new List<ItemRow>(items.Count);
        var originalIndex = new List<int>(items.Count);

        for (var i = 0; i < items.Count; i++)
        {
            var (row, error) = ItemValidator.Validate(items[i]);
            if (row is null)
            {
                results[i] = new BatchItemResult(i, items[i]?.Sku, ItemOutcome.Rejected, error);
                continue;
            }

            valid.Add(row);
            originalIndex.Add(i);
        }

        // One round trip for every valid row; invalid rows never reach the database.
        foreach (var stored in await repository.UpsertBatchAsync(tenantId, valid, cancellationToken))
        {
            var i = originalIndex[stored.Index];
            var sku = valid[stored.Index].Sku;
            results[i] = stored.Outcome switch
            {
                UpsertOutcome.Created => new BatchItemResult(i, sku, ItemOutcome.Created),
                UpsertOutcome.Updated => new BatchItemResult(i, sku, ItemOutcome.Updated),
                _ => new BatchItemResult(i, sku, ItemOutcome.Rejected, "duplicate sku in batch"),
            };
        }

        return new BatchResponse(
            results.Count(r => r.Outcome == ItemOutcome.Created),
            results.Count(r => r.Outcome == ItemOutcome.Updated),
            results.Count(r => r.Outcome == ItemOutcome.Rejected),
            results);
    }

    private static async Task<IResult> GetOne(string sku, HttpContext http, IItemRepository repository)
    {
        var tenantId = http.User.TenantId();

        var item = await repository.FindAsync(tenantId, sku, http.RequestAborted);
        return item is null ? Results.NotFound() : Results.Ok(item);
    }
}
