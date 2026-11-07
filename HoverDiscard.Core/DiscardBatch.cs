namespace HoverDiscard.Core;

public enum BatchResult
{
    Running, Completed, Stopped, InventoryChanged, ContextChanged, UnsafeState,
    TimedOut, RequestFailed,
}

/// <summary>A fixed inventory transaction. Only an expected slot becoming empty confirms a request.</summary>
public sealed class DiscardBatch
{
    private readonly ulong contentId;
    private readonly uint territoryId;
    private readonly SlotState[] expected;
    private readonly Queue<SlotState> remaining;
    private readonly HashSet<ItemKey> newItems;
    private readonly bool remember;
    private readonly Action<ItemKey> rememberItem;
    private readonly Action<ItemKey> completeItem;
    private SlotState? pending;
    private TimeSpan sentAt;
    private bool stopRequested;

    public BatchResult Result { get; private set; } = BatchResult.Running;
    public bool IsRunning => Result == BatchResult.Running;
    public int CompletedStacks { get; private set; }
    public int TotalStacks { get; }
    public ItemKey? PendingItem => pending?.Key;

    public DiscardBatch(InventorySnapshot snapshot, IReadOnlySet<ItemKey> included,
        IReadOnlySet<ItemKey> newItems, bool remember,
        Action<ItemKey> rememberItem, Action<ItemKey> completeItem, IReadOnlySet<uint>? whitelisted = null)
    {
        if (snapshot.ContentId == 0 || snapshot.Slots.Select(s => s.Address).Distinct().Count() != snapshot.Slots.Count)
            throw new ArgumentException("Invalid inventory snapshot.", nameof(snapshot));
        contentId = snapshot.ContentId;
        territoryId = snapshot.TerritoryId;
        expected = snapshot.Slots.ToArray();
        remaining = new Queue<SlotState>(expected.Where(s =>
            s.Key is { } key && included.Contains(key) && whitelisted?.Contains(key.ItemId) != true
            && s.CanDiscard && s.Quantity > 0));
        this.newItems = newItems.ToHashSet();
        this.remember = remember;
        this.rememberItem = rememberItem;
        this.completeItem = completeItem;
        TotalStacks = remaining.Count;
        if (TotalStacks == 0)
            Result = BatchResult.Completed;
    }

    public void Stop() => stopRequested = true;

    public void Tick(TimeSpan now, InventorySnapshot? current, bool safe, Action<SlotAddress> discard)
    {
        if (!IsRunning)
            return;
        if (current == null || current.ContentId != contentId || current.TerritoryId != territoryId)
        {
            Result = BatchResult.ContextChanged;
            return;
        }

        // Observe an already-issued request even after Stop or loss of focus.
        // Never issue another request after either condition.
        if (!safe)
            stopRequested = true;
        if (current.Slots.Count != expected.Length)
        {
            Result = BatchResult.InventoryChanged;
            return;
        }

        var removed = false;
        for (var i = 0; i < expected.Length; i++)
        {
            var actual = current.Slots[i];
            if (actual.Address != expected[i].Address)
            {
                Result = BatchResult.InventoryChanged;
                return;
            }
            if (pending?.Address == actual.Address && actual == SlotState.Empty(actual.Address))
            {
                removed = true;
                continue;
            }
            if (actual != expected[i])
            {
                Result = BatchResult.InventoryChanged;
                return;
            }
        }

        if (pending != null)
        {
            if (!removed)
            {
                if (now - sentAt >= TimeSpan.FromSeconds(5))
                    Result = BatchResult.TimedOut;
                return;
            }

            var key = pending.Key!.Value;
            var index = Array.FindIndex(expected, s => s.Address == pending.Address);
            expected[index] = SlotState.Empty(pending.Address);
            pending = null;
            CompletedStacks++;
            if (remember && newItems.Contains(key))
                rememberItem(key);
            if (!remaining.Any(s => s.Key == key))
                completeItem(key);
        }

        if (stopRequested)
        {
            Result = safe ? BatchResult.Stopped : BatchResult.UnsafeState;
            return;
        }
        if (!remaining.TryDequeue(out var next))
        {
            Result = BatchResult.Completed;
            return;
        }

        pending = next;
        sentAt = now;
        try
        {
            discard(next.Address);
        }
        catch
        {
            Result = BatchResult.RequestFailed;
        }
    }
}
