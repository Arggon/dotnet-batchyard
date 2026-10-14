using System.Text.Json.Serialization;

namespace Ingest.Api.Items;

public sealed record ItemRequest(string? Sku, string? Name, decimal? Price, string? Currency, int? Quantity);

public sealed record BatchRequest(IReadOnlyList<ItemRequest>? Items);

[JsonConverter(typeof(JsonStringEnumConverter<ItemOutcome>))]
public enum ItemOutcome
{
    [JsonStringEnumMemberName("created")] Created,
    [JsonStringEnumMemberName("updated")] Updated,
    [JsonStringEnumMemberName("rejected")] Rejected,
}

public sealed record ItemResponse(string Sku, ItemOutcome Outcome);

public sealed record BatchItemResult(
    int Index,
    string? Sku,
    ItemOutcome Outcome,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Reason = null);

public sealed record BatchResponse(int Created, int Updated, int Rejected, IReadOnlyList<BatchItemResult> Results);
