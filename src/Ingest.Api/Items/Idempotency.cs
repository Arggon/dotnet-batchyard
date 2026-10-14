using System.Security.Cryptography;
using System.Text.Json;
using Ingest.Data;

namespace Ingest.Api.Items;

/// <summary>Replays stored responses for a repeated Idempotency-Key (ADR 0005).</summary>
internal static class Idempotency
{
    public const string HeaderName = "Idempotency-Key";
    private const int MaxKeyLength = 128;

    public static async Task<IResult> RunAsync<TRequest>(
        HttpContext http,
        IIdempotencyStore store,
        Guid tenantId,
        TRequest request,
        Func<Task<(int StatusCode, object Body)>> handler)
    {
        var key = http.Request.Headers[HeaderName].ToString();
        if (string.IsNullOrEmpty(key))
        {
            var (status, body) = await handler();
            return Results.Json(body, statusCode: status);
        }

        if (key.Length > MaxKeyLength)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid Idempotency-Key",
                detail: $"The key must be at most {MaxKeyLength} characters.");
        }

        var hash = SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(request, JsonOptions));
        var stored = await store.FindAsync(tenantId, key, http.RequestAborted);
        if (stored is null)
        {
            var (status, body) = await handler();
            var json = JsonSerializer.Serialize(body, body.GetType(), JsonOptions);
            if (status < 500)
            {
                await store.TrySaveAsync(tenantId, key, new StoredResponse(hash, status, json), http.RequestAborted);
            }

            return Results.Text(json, "application/json", statusCode: status);
        }

        if (!CryptographicOperations.FixedTimeEquals(stored.RequestHash, hash))
        {
            return Results.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Idempotency-Key reused",
                detail: "This key was already used with a different request body.");
        }

        http.Response.Headers["Idempotent-Replayed"] = "true";
        return Results.Text(stored.ResponseBody, "application/json", statusCode: stored.StatusCode);
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
}
