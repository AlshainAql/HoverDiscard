using HoverDiscard.Core;
using Xunit;

namespace HoverDiscard.Tests;

public sealed class HoverSelectionTests
{
    private static InventorySnapshot Inventory(ItemQuality quality = ItemQuality.Normal) =>
        new(42, 7, [new(new(3, 34), new ItemKey(100, quality), 20, "", true)]);

    private static SlotAddress[] DisplayOrder() =>
        Enumerable.Range(0, 140).Select(i => new SlotAddress(i / 35, i % 35)).ToArray();

    [Theory]
    [InlineData("Inventory")]
    [InlineData("InventoryLarge")]
    [InlineData("InventoryExpansion")]
    [InlineData("InventoryGrid")]
    [InlineData("InventoryGrid0")]
    [InlineData("InventoryGrid1")]
    [InlineData("InventoryGrid2")]
    [InlineData("InventoryGrid3")]
    [InlineData("InventoryGrid0E")]
    [InlineData("InventoryGrid1E")]
    [InlineData("InventoryGrid2E")]
    [InlineData("InventoryGrid3E")]
    public void ResolvesInventoryLayoutsToTheActualBagSlot(string addon)
    {
        Assert.Equal(new ItemKey(100, ItemQuality.Normal),
            HoverSelection.Resolve(Inventory(), true, 51, 34, 100, 100, addon, DisplayOrder()));
    }

    [Theory]
    [InlineData("InventoryGrid0")]
    [InlineData("InventoryGrid0E")]
    public void ResolvesSortedDisplayPositionInsteadOfTheUnrelatedPhysicalSlot(string addon)
    {
        // Observed in-game: display page 0, index 9 points to bag 3, slot 26.
        var expected = new ItemKey(33928, ItemQuality.Normal);
        var inventory = new InventorySnapshot(42, 7,
        [
            new(new(0, 9), new ItemKey(19921, ItemQuality.Normal), 12, "", true),
            new(new(3, 26), expected, 5, "", true),
        ]);
        var order = DisplayOrder();
        (order[9], order[131]) = (order[131], order[9]);
        Assert.Equal(expected, HoverSelection.Resolve(inventory, true, 48, 9, 33928, 33928, addon, order));
        Assert.Null(HoverSelection.Resolve(inventory, true, 48, 9, 33928, 33928, addon, DisplayOrder()));
        Assert.Null(HoverSelection.Resolve(inventory, true, 48, 9, 19921, 19921, addon, order));
    }

    [Fact]
    public void ResolvesSortedHighQualityItemWhenThePhysicalDisplayAddressIsEmpty()
    {
        // Observed in-game: display page 2, index 5 points to bag 1, slot 30.
        var expected = new ItemKey(42508, ItemQuality.High);
        var inventory = new InventorySnapshot(42, 7,
        [
            SlotState.Empty(new(2, 5)),
            new(new(1, 30), expected, 1, "", true),
        ]);
        var order = DisplayOrder();
        (order[75], order[65]) = (order[65], order[75]);
        Assert.Equal(expected,
            HoverSelection.Resolve(inventory, true, 50, 5, 1042508, 1042508, "InventoryGrid2E", order));
    }

    [Theory]
    [InlineData(100, ItemQuality.Normal)]
    [InlineData(1000100, ItemQuality.High)]
    [InlineData(500100, ItemQuality.Collectable)]
    public void ResolvesEncodedTooltipIdsWithoutMixingQualities(uint encoded, ItemQuality quality)
    {
        Assert.Equal(new ItemKey(100, quality),
            HoverSelection.Resolve(Inventory(quality), true, 51, 34, encoded, encoded, "Inventory", DisplayOrder()));
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
    [InlineData(true, 51, 34, 100, "InventoryRetainer")]
    [InlineData(true, 51, 34, 100, null)]
    public void RejectsNonInventoryOriginsAndMismatchedTooltips(bool inventoryItem, uint type,
        uint index, uint hovered, string? addon)
    {
        Assert.Null(HoverSelection.Resolve(Inventory(), inventoryItem, type, index, hovered, 100, addon, DisplayOrder()));
    }

    [Fact]
    public void RejectsStaleDetailsAndIndisposableItems()
    {
        var inventory = Inventory();
        Assert.Null(HoverSelection.Resolve(inventory, true, 51, 34, 100, 101, "Inventory", DisplayOrder()));
        inventory = inventory with { Slots = [inventory.Slots[0] with { CanDiscard = false }] };
        Assert.Null(HoverSelection.Resolve(inventory, true, 51, 34, 100, 100, "Inventory", DisplayOrder()));
    }

    [Fact]
    public void RejectsMissingDisplayOrder()
    {
        Assert.Null(HoverSelection.Resolve(Inventory(), true, 51, 34, 100, 100, "Inventory", []));
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(4, 0)]
    [InlineData(0, -1)]
    [InlineData(0, 35)]
    public void RejectsInvalidMappedSlots(int page, int slot)
    {
        var order = DisplayOrder();
        order[139] = new(page, slot);
        Assert.Null(HoverSelection.Resolve(Inventory(), true, 51, 34, 100, 100, "Inventory", order));
    }
}
