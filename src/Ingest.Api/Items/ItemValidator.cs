using System.Text.RegularExpressions;
using Ingest.Data;

namespace Ingest.Api.Items;

/// <summary>In-memory validation, run before anything reaches the database (spec ingest-batch-001).</summary>
public static partial class ItemValidator
{
    public const int MaxSkuLength = 64;
    public const int MaxNameLength = 200;

    /// <summary>Returns the item ready to store, or the reason it was rejected.</summary>
    public static (ItemRow? Row, string? Error) Validate(ItemRequest? item)
    {
        if (item is null)
        {
            return (null, "item is required");
        }

        if (string.IsNullOrWhiteSpace(item.Sku))
        {
            return (null, "sku is required");
        }

        if (item.Sku.Length > MaxSkuLength || !SkuPattern().IsMatch(item.Sku))
        {
            return (null, $"sku must be 1-{MaxSkuLength} characters of letters, digits, '.', '_' or '-'");
        }

        if (string.IsNullOrWhiteSpace(item.Name))
        {
            return (null, "name is required");
        }

        if (item.Name.Length > MaxNameLength)
        {
            return (null, $"name must be at most {MaxNameLength} characters");
        }

        if (item.Price is not { } price || price < 0)
        {
            return (null, "price must be zero or more");
        }

        if (decimal.Round(price, 4) != price || price > 999_999_999_999_999m)
        {
            return (null, "price must fit decimal(19,4)");
        }

        if (item.Currency is null || !CurrencyPattern().IsMatch(item.Currency))
        {
            return (null, "currency must be a 3-letter upper-case ISO 4217 code");
        }

        if (item.Quantity is not { } quantity || quantity < 0)
        {
            return (null, "quantity must be zero or more");
        }

        return (new ItemRow(item.Sku, item.Name.Trim(), price, item.Currency, quantity), null);
    }

    [GeneratedRegex("^[A-Za-z0-9._-]+$")]
    private static partial Regex SkuPattern();

    [GeneratedRegex("^[A-Z]{3}$")]
    private static partial Regex CurrencyPattern();
}
