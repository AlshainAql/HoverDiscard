namespace HoverDiscard.Core;

/// <summary>Validates tooltip metadata against a slot in the four main inventory pages.</summary>
public static class HoverSelection
{
    public static ItemKey? Resolve(InventorySnapshot inventory, bool isInventoryItem,
        uint typeOrId, uint index, ulong hoveredId, uint detailId, string? intersectingAddon)
    {
        if (!isInventoryItem || typeOrId is < 48 or > 51 || index >= 35 || hoveredId == 0)
            return null;
        if (intersectingAddon is not ("Inventory" or "InventoryLarge" or "InventoryExpansion"
            or "InventoryGrid" or "InventoryGrid0" or "InventoryGrid1" or "InventoryGrid2" or "InventoryGrid3"))
            return null;
        var address = new SlotAddress((int)(typeOrId - 48), (int)index);
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
