using HoverDiscard.Core;
using Xunit;

namespace HoverDiscard.Tests;

public sealed class WhitelistTests
{
    private static readonly ItemKey Normal = new(100, ItemQuality.Normal);
    private static readonly ItemKey High = new(100, ItemQuality.High);
    private static readonly ItemKey Collectable = new(100, ItemQuality.Collectable);
    private static readonly ItemKey Other = new(200, ItemQuality.Normal);

    [Theory]
    [InlineData(ItemQuality.Normal)]
    [InlineData(ItemQuality.High)]
    [InlineData(ItemQuality.Collectable)]
    public void WhitelistPreventsHoverSelectionForEveryQuality(ItemQuality quality)
    {
        var key = new ItemKey(100, quality);
        var selection = new Selection();
        var saved = new HashSet<ItemKey> { key };
        var protectedIds = new HashSet<uint> { 100 };
        selection.Excluded.Add(key);
        selection.Add(key, saved, protectedIds);
        Assert.Contains(key, selection.Excluded);
        Assert.Empty(selection.NewItems);
        Assert.Empty(selection.Included(saved, protectedIds));
        selection.Add(key, new HashSet<ItemKey>(), protectedIds);
        Assert.Empty(selection.NewItems);
    }

    [Fact]
    public void ProtectionOverridesSavedAndStaleNewSelectionsWithoutDeletingRules()
    {
        var settings = new CharacterSettings
        {
            RememberedItems = [Normal, High, Collectable, Other],
            WhitelistedItems = [100],
        };
        var selection = new Selection();
        selection.NewItems.UnionWith([Normal, High, Collectable, Other]);
        var inventory = new InventorySnapshot(42, 7,
        [
            new(new(0, 0), Normal, 99, "", true),
            new(new(0, 1), High, 5, "", true),
            new(new(0, 2), Collectable, 1, "", true),
            new(new(0, 3), Other, 10, "", true),
        ]);
        var rows = selection.Rows(inventory, settings.RememberedItems, settings.WhitelistedItems);
        Assert.Equal(4, rows.Count);
        Assert.All(rows.Where(r => r.Key.ItemId == 100), row =>
        {
            Assert.True(row.Saved);
            Assert.False(row.Selected);
        });
        Assert.Equal([Other], selection.Included(settings.RememberedItems, settings.WhitelistedItems));
        Assert.Equal(4, settings.RememberedItems.Count);

        // Even a caller passing stale checked items cannot enqueue protected stacks.
        var requests = new List<SlotAddress>();
        var batch = new DiscardBatch(inventory, settings.RememberedItems, selection.NewItems,
            true, _ => Assert.Fail("Protected item was remembered."), _ => { }, settings.WhitelistedItems);
        Assert.Equal(1, batch.TotalStacks);
        batch.Tick(TimeSpan.Zero, inventory, true, requests.Add);
        Assert.Equal([new SlotAddress(0, 3)], requests);
    }

    [Fact]
    public void RemovingProtectionRestoresSavedRulesButNotClearedNewSelections()
    {
        var selection = new Selection();
        var saved = new HashSet<ItemKey> { High };
        var protectedIds = new HashSet<uint>();
        selection.Add(Normal, saved, protectedIds);
        selection.Excluded.Add(High);
        protectedIds.Add(100);
        selection.RemoveItem(100);
        Assert.Empty(selection.NewItems);
        Assert.Empty(selection.Excluded);
        Assert.Empty(selection.Included(saved, protectedIds));
        protectedIds.Remove(100);
        Assert.Equal([High], selection.Included(saved, protectedIds));
        Assert.DoesNotContain(Normal, selection.Included(saved, protectedIds));
    }

    [Fact]
    public void OnlyVisibleCheckedItemsCanBeDiscardedWithoutChangingOtherSelections()
    {
        var selection = new Selection();
        var saved = new HashSet<ItemKey> { Normal, High, Other };
        var protectedIds = new HashSet<uint>();
        selection.Excluded.Add(High);
        Assert.Equal([Normal], selection.Included(saved, protectedIds, new HashSet<ItemKey> { Normal, High }));
        Assert.Equal([Other], selection.Included(saved, protectedIds, new HashSet<ItemKey> { Other }));
        Assert.Empty(selection.Included(saved, protectedIds, new HashSet<ItemKey>()));
        Assert.Equal(2, selection.Included(saved, protectedIds).Count);
        Assert.Contains(High, selection.Excluded);
        protectedIds.Add(100);
        Assert.Empty(selection.Included(saved, protectedIds, new HashSet<ItemKey> { Normal, High }));
    }

    [Fact]
    public void WhitelistRoundTripIsPerCharacterAndKeepsAbsentItems()
    {
        WithPath(path =>
        {
            var config = new Configuration();
            config.ForCharacter(42).WhitelistedItems.Add(100);
            config.ForCharacter(42).RememberedItems.Add(High);
            config.ForCharacter(43).WhitelistedItems.Add(200);
            ConfigurationFile.Save(path, config);
            var restored = ConfigurationFile.Load(path, UiLanguage.Japanese);
            Assert.Equal([100u], restored.ForCharacter(42).WhitelistedItems);
            Assert.Equal([200u], restored.ForCharacter(43).WhitelistedItems);
            Assert.Empty(restored.ForCharacter(44).WhitelistedItems);
            var row = Assert.Single(new Selection().Rows(null, restored.ForCharacter(42).RememberedItems,
                restored.ForCharacter(42).WhitelistedItems));
            Assert.Equal(0, row.Quantity);
            Assert.True(row.Saved);
            Assert.False(row.Selected);
        });
    }

    [Fact]
    public void PreviousConfigurationLoadsWithoutLosingRulesOrPreferences()
    {
        WithPath(path =>
        {
            const string original = """
                {"Version":1,"Language":"Japanese","SelectionKey":"Ctrl","Characters":{"42":{"RememberNewItems":false,"RememberedItems":[{"ItemId":100,"Quality":"High"}]}}}
                """;
            File.WriteAllText(path, original);
            var config = ConfigurationFile.Load(path, UiLanguage.English);
            Assert.Equal(2, config.Version);
            Assert.Equal(UiLanguage.Japanese, config.Language);
            Assert.Equal(SelectionKey.Ctrl, config.SelectionKey);
            Assert.False(config.ForCharacter(42).RememberNewItems);
            Assert.Equal([High], config.ForCharacter(42).RememberedItems);
            Assert.Empty(config.ForCharacter(42).WhitelistedItems);
            Assert.Equal(original, File.ReadAllText(path));
            ConfigurationFile.Save(path, config);
            Assert.Equal([High], ConfigurationFile.Load(path, UiLanguage.English).ForCharacter(42).RememberedItems);
        });
    }

    [Theory]
    [InlineData("""{"Characters":{"42":{"WhitelistedItems":null}}}""")]
    [InlineData("""{"Characters":{"42":{"WhitelistedItems":[0]}}}""")]
    [InlineData("""{"Characters":{"42":{"WhitelistedItems":[-1]}}}""")]
    public void InvalidWhitelistDoesNotOverwriteConfiguration(string json)
    {
        WithPath(path =>
        {
            File.WriteAllText(path, json);
            Assert.ThrowsAny<Exception>(() => ConfigurationFile.Load(path, UiLanguage.English));
            Assert.Equal(json, File.ReadAllText(path));
        });
    }

    [Fact]
    public void WhitelistHoverCanProtectIndisposableItemsButStillRejectsStaleTooltips()
    {
        var inventory = new InventorySnapshot(42, 7, [new(new(0, 0), Normal, 1, "", false)]);
        var order = Enumerable.Range(0, 140).Select(i => new SlotAddress(i / 35, i % 35)).ToArray();
        Assert.Null(HoverSelection.Resolve(inventory, true, 48, 0, 100, 100, "InventoryGrid0E", order));
        Assert.Equal(Normal, HoverSelection.Resolve(inventory, true, 48, 0, 100, 100, "InventoryGrid0E", order, false));
        Assert.Null(HoverSelection.Resolve(inventory, true, 48, 0, 100, 200, "InventoryGrid0E", order, false));
        Assert.Null(HoverSelection.Resolve(inventory, true, 48, 0, 100, 100, "Shop", order, false));
    }

    private static void WithPath(Action<string> action)
    {
        var directory = Path.Combine(Path.GetTempPath(), "HoverDiscard.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try { action(Path.Combine(directory, "configuration.json")); }
        finally
        {
            File.Delete(Path.Combine(directory, "configuration.json"));
            File.Delete(Path.Combine(directory, "configuration.json.tmp"));
            Directory.Delete(directory);
        }
    }
}
