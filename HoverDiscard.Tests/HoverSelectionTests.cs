using HoverDiscard.Core;
using Xunit;

namespace HoverDiscard.Tests;

public sealed class HoverSelectionTests
{
    private static InventorySnapshot Inventory(ItemQuality quality = ItemQuality.Normal) =>
        new(42, 7, [new(new(3, 34), new ItemKey(100, quality), 20, "", true)]);

    [Theory]
    [InlineData("Inventory")]
    [InlineData("InventoryLarge")]
    [InlineData("InventoryExpansion")]
    [InlineData("InventoryGrid3")]
    public void ResolvesInventoryLayoutsToTheActualBagSlot(string addon)
    {
        Assert.Equal(new ItemKey(100, ItemQuality.Normal),
            HoverSelection.Resolve(Inventory(), true, 51, 34, 100, 100, addon));
    }

    [Theory]
    [InlineData(100, ItemQuality.Normal)]
    [InlineData(1000100, ItemQuality.High)]
    [InlineData(500100, ItemQuality.Collectable)]
    public void ResolvesEncodedTooltipIdsWithoutMixingQualities(uint encoded, ItemQuality quality)
    {
        Assert.Equal(new ItemKey(100, quality),
            HoverSelection.Resolve(Inventory(quality), true, 51, 34, encoded, encoded, "Inventory"));
    }

    [Theory]
    [InlineData(false, 51, 34, 100, "Inventory")]
    [InlineData(true, 4, 34, 100, "Inventory")]
    [InlineData(true, 7, 34, 100, "Inventory")]
    [InlineData(true, 69, 34, 100, "Inventory")]
    [InlineData(true, 51, 35, 100, "Inventory")]
    [InlineData(true, 51, 34, 101, "Inventory")]
    [InlineData(true, 51, 34, 0, "Inventory")]
    [InlineData(true, 51, 34, 100, "ChatLog")]
    [InlineData(true, 51, 34, 100, "Shop")]
    [InlineData(true, 51, 34, 100, "ArmouryBoard")]
    [InlineData(true, 51, 34, 100, null)]
    public void RejectsNonInventoryOriginsAndMismatchedTooltips(bool inventoryItem, uint type,
        uint index, uint hovered, string? addon)
    {
        Assert.Null(HoverSelection.Resolve(Inventory(), inventoryItem, type, index, hovered, 100, addon));
    }

    [Fact]
    public void RejectsStaleDetailsAndIndisposableItems()
    {
        var inventory = Inventory();
        Assert.Null(HoverSelection.Resolve(inventory, true, 51, 34, 100, 101, "Inventory"));
        inventory = inventory with { Slots = [inventory.Slots[0] with { CanDiscard = false }] };
        Assert.Null(HoverSelection.Resolve(inventory, true, 51, 34, 100, 100, "Inventory"));
    }
}
