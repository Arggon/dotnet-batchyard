using Ingest.Api.Items;

namespace Ingest.UnitTests;

public class ItemValidatorTests
{
    private static ItemRequest Valid() => new("BRK-001", "Brake pad", 12.50m, "USD", 40);

    [Fact]
    public void Valid_item_becomes_a_row()
    {
        var (row, error) = ItemValidator.Validate(Valid());

        error.ShouldBeNull();
        row.ShouldNotBeNull();
        row.Sku.ShouldBe("BRK-001");
        row.Price.ShouldBe(12.50m);
    }

    public static TheoryData<ItemRequest, string> InvalidItems => new()
    {
        { Valid() with { Sku = null }, "sku is required" },
        { Valid() with { Sku = "" }, "sku is required" },
        { Valid() with { Sku = "has space" }, "sku must be" },
        { Valid() with { Sku = "'; DROP TABLE Items;--" }, "sku must be" },
        { Valid() with { Sku = new string('A', 65) }, "sku must be" },
        { Valid() with { Name = " " }, "name is required" },
        { Valid() with { Name = new string('n', 201) }, "name must be at most" },
        { Valid() with { Price = null }, "price must be zero or more" },
        { Valid() with { Price = -0.01m }, "price must be zero or more" },
        { Valid() with { Price = 1.00001m }, "price must fit decimal(19,4)" },
        { Valid() with { Currency = "usd" }, "currency must be" },
        { Valid() with { Currency = "US" }, "currency must be" },
        { Valid() with { Quantity = -1 }, "quantity must be zero or more" },
        { Valid() with { Quantity = null }, "quantity must be zero or more" },
    };

    [Theory]
    [MemberData(nameof(InvalidItems))]
    public void Invalid_item_is_rejected_with_a_reason(ItemRequest item, string expectedReason)
    {
        var (row, error) = ItemValidator.Validate(item);

        row.ShouldBeNull();
        error.ShouldNotBeNull();
        error.ShouldStartWith(expectedReason);
    }

    [Fact]
    public void Null_item_is_rejected()
    {
        ItemValidator.Validate(null).Error.ShouldBe("item is required");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(999_999_999_999_999)]
    public void Price_bounds_are_accepted(long price)
    {
        ItemValidator.Validate(Valid() with { Price = price }).Error.ShouldBeNull();
    }
}
