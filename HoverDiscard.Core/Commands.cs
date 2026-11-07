namespace HoverDiscard.Core;

public enum PluginCommand { Open, Discard, Stop, Help, Unknown }

public static class CommandInput
{
    public static PluginCommand Parse(string arguments) => arguments.Trim().ToLowerInvariant() switch
    {
        "" => PluginCommand.Open,
        "discard" => PluginCommand.Discard,
        "stop" => PluginCommand.Stop,
        "help" => PluginCommand.Help,
        _ => PluginCommand.Unknown,
    };
}

/// <summary>A one-shot request bound to the character and area that invoked it.</summary>
public sealed record RememberedDiscardRequest(ulong ContentId, uint TerritoryId)
{
    public static HashSet<ItemKey> Targets(CharacterSettings settings, IReadOnlySet<ItemKey> excluded) =>
        settings.RememberedItems.Except(excluded)
            .Where(key => !settings.WhitelistedItems.Contains(key.ItemId)).ToHashSet();

    public DiscardBatch? Prepare(InventorySnapshot? inventory, CharacterSettings settings,
        IReadOnlySet<ItemKey> excluded, bool safe, Action<ItemKey> completeItem, out TextId? error)
    {
        error = null;
        if (inventory == null)
            error = TextId.InventoryUnavailable;
        else if (ContentId == 0 || inventory.ContentId != ContentId || inventory.TerritoryId != TerritoryId)
            error = TextId.ContextChanged;
        else if (!safe)
            error = TextId.Unsafe;
        if (error != null)
            return null;

        var batch = new DiscardBatch(inventory!, Targets(settings, excluded), new HashSet<ItemKey>(),
            false, _ => { }, completeItem, settings.WhitelistedItems);
        if (batch.TotalStacks != 0)
            return batch;
        error = TextId.NoRememberedTargets;
        return null;
    }
}
