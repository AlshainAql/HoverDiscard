using HoverDiscard.Core;
using Xunit;

namespace HoverDiscard.Tests;

public sealed class CommandTests
{
    private static readonly ItemKey Normal = new(100, ItemQuality.Normal);
    private static readonly ItemKey High = new(100, ItemQuality.High);
    private static readonly ItemKey Protected = new(200, ItemQuality.Normal);
    private static readonly ItemKey Excluded = new(300, ItemQuality.Normal);
    private static readonly ItemKey Fresh = new(400, ItemQuality.Normal);
    private static SlotState Slot(int slot, ItemKey key, int quantity = 1) => new(new(0, slot), key, quantity, "", true);
    private static InventorySnapshot Snapshot(params SlotState[] slots) => new(42, 7, slots);

    [Theory]
    [InlineData("", PluginCommand.Open)]
    [InlineData("  ", PluginCommand.Open)]
    [InlineData("discard", PluginCommand.Discard)]
    [InlineData(" DISCARD ", PluginCommand.Discard)]
    [InlineData("stop", PluginCommand.Stop)]
    [InlineData("Stop", PluginCommand.Stop)]
    [InlineData("help", PluginCommand.Help)]
    [InlineData("discard all", PluginCommand.Unknown)]
    [InlineData("discard\nstop", PluginCommand.Unknown)]
    [InlineData("discards", PluginCommand.Unknown)]
    [InlineData("delete", PluginCommand.Unknown)]
    public void OnlyExactSubcommandsAreAccepted(string input, PluginCommand expected) =>
        Assert.Equal(expected, CommandInput.Parse(input));

    [Fact]
    public void CommandTargetsOnlyHeldRememberedQualityAndHonorsProtectionAndExclusions()
    {
        var settings = new CharacterSettings { RememberedItems = [Normal, Protected, Excluded], WhitelistedItems = [200] };
        var inventory = Snapshot(Slot(0, Normal, 99), Slot(1, Normal, 20), Slot(2, High),
            Slot(3, Protected), Slot(4, Excluded), Slot(5, Fresh));
        var completed = new List<ItemKey>();
        var batch = new RememberedDiscardRequest(42, 7).Prepare(inventory, settings, new HashSet<ItemKey> { Excluded },
            true, completed.Add, out var error);
        Assert.Null(error);
        Assert.NotNull(batch);
        Assert.Equal(2, batch.TotalStacks);
        var requests = new List<SlotAddress>();
        batch.Tick(TimeSpan.Zero, inventory, true, requests.Add);
        Assert.Equal([new SlotAddress(0, 0)], requests);
        var afterFirst = inventory with { Slots = inventory.Slots.Select((s, i) => i == 0 ? SlotState.Empty(s.Address) : s).ToArray() };
        batch.Tick(TimeSpan.FromSeconds(1), afterFirst, true, requests.Add);
        Assert.Equal([new SlotAddress(0, 0), new SlotAddress(0, 1)], requests);
        var afterSecond = afterFirst with { Slots = afterFirst.Slots.Select((s, i) => i == 1 ? SlotState.Empty(s.Address) : s).ToArray() };
        batch.Tick(TimeSpan.FromSeconds(2), afterSecond, true, requests.Add);
        Assert.Equal(BatchResult.Completed, batch.Result);
        Assert.Equal([Normal], completed);
        Assert.Equal(3, settings.RememberedItems.Count);
        Assert.DoesNotContain(High, settings.RememberedItems);
        Assert.DoesNotContain(Fresh, settings.RememberedItems);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RememberToggleDoesNotDisableExistingCommandRules(bool remember)
    {
        var settings = new CharacterSettings { RememberNewItems = remember, RememberedItems = [Normal] };
        var batch = new RememberedDiscardRequest(42, 7).Prepare(Snapshot(Slot(0, Normal)),
            settings, new HashSet<ItemKey>(), true, _ => { }, out var error);
        Assert.Null(error);
        Assert.NotNull(batch);
        Assert.Equal(1, batch.TotalStacks);
    }

    [Theory]
    [InlineData("empty")]
    [InlineData("absent")]
    [InlineData("protected")]
    [InlineData("unchecked")]
    [InlineData("indisposable")]
    [InlineData("zero-quantity")]
    public void NoEligibleRememberedTargetsNeverCreatesABatch(string reason)
    {
        var settings = new CharacterSettings { RememberedItems = reason == "empty" ? [] : [Normal] };
        var excluded = new HashSet<ItemKey>();
        if (reason == "protected")
            settings.WhitelistedItems.Add(100);
        if (reason == "unchecked")
            excluded.Add(Normal);
        var slot = Slot(0, reason == "absent" ? Fresh : Normal);
        if (reason == "indisposable")
            slot = slot with { CanDiscard = false };
        if (reason == "zero-quantity")
            slot = slot with { Quantity = 0 };
        var batch = new RememberedDiscardRequest(42, 7).Prepare(Snapshot(slot), settings, excluded,
            true, _ => Assert.Fail("An item completed without a batch."), out var error);
        Assert.Null(batch);
        Assert.Equal(TextId.NoRememberedTargets, error);
    }

    [Theory]
    [InlineData("character", TextId.ContextChanged)]
    [InlineData("area", TextId.ContextChanged)]
    [InlineData("logout", TextId.ContextChanged)]
    [InlineData("invalid-request", TextId.ContextChanged)]
    [InlineData("unavailable", TextId.InventoryUnavailable)]
    [InlineData("unsafe", TextId.Unsafe)]
    public void InvalidOrChangedContextRejectsTheRequest(string change, TextId expected)
    {
        var settings = new CharacterSettings { RememberedItems = [Normal] };
        InventorySnapshot? inventory = Snapshot(Slot(0, Normal));
        inventory = change switch
        {
            "character" => inventory with { ContentId = 43 },
            "area" => inventory with { TerritoryId = 8 },
            "logout" => inventory with { ContentId = 0 },
            "unavailable" => null,
            _ => inventory,
        };
        var request = new RememberedDiscardRequest(change == "invalid-request" ? 0ul : 42ul, 7);
        Assert.Null(request.Prepare(inventory, settings, new HashSet<ItemKey>(), change != "unsafe", _ => { }, out var error));
        Assert.Equal(expected, error);
    }

    [Fact]
    public void FreshInventoryIsRevalidatedBeforeTheFirstRequest()
    {
        var settings = new CharacterSettings { RememberedItems = [Normal] };
        var batch = new RememberedDiscardRequest(42, 7).Prepare(Snapshot(Slot(0, Normal, 99)),
            settings, new HashSet<ItemKey>(), true, _ => Assert.Fail("Unexpected completion."), out var error);
        Assert.Null(error);
        Assert.NotNull(batch);
        batch.Tick(TimeSpan.Zero, Snapshot(Slot(0, Fresh, 99)), true, _ => Assert.Fail("A changed slot was discarded."));
        Assert.Equal(BatchResult.InventoryChanged, batch.Result);
    }

    [Fact]
    public void StopBeforeTheFirstRequestDoesNotDiscardAnything()
    {
        var settings = new CharacterSettings { RememberedItems = [Normal] };
        var inventory = Snapshot(Slot(0, Normal));
        var batch = new RememberedDiscardRequest(42, 7).Prepare(inventory,
            settings, new HashSet<ItemKey>(), true, _ => { }, out _);
        Assert.NotNull(batch);
        batch.Stop();
        batch.Tick(TimeSpan.Zero, inventory, true, _ => Assert.Fail("Discarded after stop."));
        Assert.Equal(BatchResult.Stopped, batch.Result);
    }
}
