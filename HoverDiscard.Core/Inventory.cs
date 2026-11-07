namespace HoverDiscard.Core;

public enum ItemQuality { Normal, High, Collectable }

public readonly record struct ItemKey(uint ItemId, ItemQuality Quality);
public readonly record struct SlotAddress(int Page, int Slot);

public sealed record SlotState(
    SlotAddress Address,
    ItemKey? Key,
    int Quantity,
    string Fingerprint,
    bool CanDiscard)
{
    public static SlotState Empty(SlotAddress address) => new(address, null, 0, "", false);
}

public sealed record InventorySnapshot(ulong ContentId, uint TerritoryId, IReadOnlyList<SlotState> Slots);

public sealed record ItemRow(ItemKey Key, int Quantity, int Stacks, bool Saved, bool Selected, bool CanDiscard);

public sealed class Selection
{
    public HashSet<ItemKey> NewItems { get; } = [];
    public HashSet<ItemKey> Excluded { get; } = [];

    public void Add(ItemKey key, IReadOnlySet<ItemKey> saved, IReadOnlySet<uint> whitelisted)
    {
        if (whitelisted.Contains(key.ItemId))
            return;
        if (!saved.Contains(key))
            NewItems.Add(key);
        Excluded.Remove(key);
    }

    public void RemoveItem(uint itemId)
    {
        NewItems.RemoveWhere(key => key.ItemId == itemId);
        Excluded.RemoveWhere(key => key.ItemId == itemId);
    }

    public void Reset()
    {
        NewItems.Clear();
        Excluded.Clear();
    }

    public IReadOnlyList<ItemRow> Rows(InventorySnapshot? inventory, IReadOnlySet<ItemKey> saved,
        IReadOnlySet<uint> whitelisted)
    {
        var slots = inventory?.Slots ?? [];
        return NewItems.Union(saved).Select(key =>
        {
            var matches = slots.Where(s => s.Key == key).ToArray();
            return new ItemRow(key, matches.Sum(s => s.Quantity), matches.Length,
                saved.Contains(key), !Excluded.Contains(key) && !whitelisted.Contains(key.ItemId),
                matches.Length > 0 && matches.All(s => s.CanDiscard));
        }).OrderBy(r => r.Key.ItemId).ThenBy(r => r.Key.Quality).ToArray();
    }

    public HashSet<ItemKey> Included(IReadOnlySet<ItemKey> saved, IReadOnlySet<uint> whitelisted,
        IReadOnlySet<ItemKey>? visible = null) =>
        NewItems.Union(saved).Except(Excluded)
            .Where(key => !whitelisted.Contains(key.ItemId) && (visible == null || visible.Contains(key)))
            .ToHashSet();
}
