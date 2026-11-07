namespace HoverDiscard.Core;

/// <summary>Validates tooltip metadata against the inventory's displayed slot order.</summary>
public static class HoverSelection
{
    public static ItemKey? Resolve(InventorySnapshot inventory, bool isInventoryItem,
        uint typeOrId, uint index, ulong hoveredId, uint detailId, string? intersectingAddon,
        IReadOnlyList<SlotAddress> displayOrder)
    {
        if (!isInventoryItem || typeOrId is < 48 or > 51 || index >= 35 || hoveredId == 0
            || displayOrder.Count != 140)
            return null;
        if (intersectingAddon is not ("Inventory" or "InventoryLarge" or "InventoryExpansion"
            or "InventoryGrid" or "InventoryGrid0" or "InventoryGrid1" or "InventoryGrid2" or "InventoryGrid3"
            or "InventoryGrid0E" or "InventoryGrid1E" or "InventoryGrid2E" or "InventoryGrid3E"))
            return null;
        // Tooltip coordinates refer to display positions, which differ from bag slots after sorting.
        var displayIndex = (int)((typeOrId - 48) * 35 + index);
        var address = displayOrder[displayIndex];
        if (address.Page is < 0 or > 3 || address.Slot is < 0 or >= 35)
            return null;
        var slot = inventory.Slots.FirstOrDefault(s => s.Address == address);
        if (slot is not { CanDiscard: true, Quantity: > 0, Key: { } key })
            return null;
        return BaseItemId(hoveredId) == key.ItemId && BaseItemId(detailId) == key.ItemId ? key : null;
    }

    private static ulong BaseItemId(ulong id) => id switch
    {
        >= 1_000_000 and < 1_500_000 => id - 1_000_000,
        >= 500_000 and < 1_000_000 => id - 500_000,
        _ => id,
    };
}
