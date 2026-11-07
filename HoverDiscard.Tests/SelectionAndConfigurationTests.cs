using HoverDiscard.Core;
using Xunit;

namespace HoverDiscard.Tests;

public sealed class SelectionAndConfigurationTests
{
    [Fact]
    public void AggregatesEveryStackButKeepsQualitySeparate()
    {
        var normal = new ItemKey(100, ItemQuality.Normal);
        var high = new ItemKey(100, ItemQuality.High);
        var collectable = new ItemKey(100, ItemQuality.Collectable);
        var selection = new Selection();
        var saved = new HashSet<ItemKey> { high, collectable };
        selection.Add(normal, saved, new HashSet<uint>());
        selection.Add(normal, saved, new HashSet<uint>());
        var inventory = new InventorySnapshot(42, 7,
        [
            new(new(0, 0), normal, 99, "", true),
            new(new(3, 34), normal, 20, "", true),
            new(new(0, 1), high, 3, "", true),
            new(new(0, 2), collectable, 1, "", true),
        ]);
        var rows = selection.Rows(inventory, saved, new HashSet<uint>());
        Assert.Equal(3, rows.Count);
        var row = Assert.Single(rows, r => r.Key == normal);
        Assert.Equal(119, row.Quantity);
        Assert.Equal(2, row.Stacks);
        Assert.False(row.Saved);
        Assert.Single(selection.NewItems);
    }

    [Fact]
    public void ExclusionDoesNotDeleteSavedRulesAndHoverRechecksThem()
    {
        var key = new ItemKey(100, ItemQuality.Normal);
        var saved = new HashSet<ItemKey> { key };
        var selection = new Selection();
        selection.Excluded.Add(key);
        Assert.Empty(selection.Included(saved, new HashSet<uint>()));
        Assert.Contains(key, saved);
        selection.Add(key, saved, new HashSet<uint>());
        Assert.Contains(key, selection.Included(saved, new HashSet<uint>()));
        Assert.Empty(selection.NewItems);
    }

    [Fact]
    public void SavedItemsRemainVisibleWhenNotHeldAndResetClearsOnlyTransientState()
    {
        var key = new ItemKey(100, ItemQuality.Normal);
        var selection = new Selection();
        var saved = new HashSet<ItemKey> { key };
        selection.NewItems.Add(new(200, ItemQuality.High));
        selection.Excluded.Add(key);
        selection.Reset();
        var row = Assert.Single(selection.Rows(null, saved, new HashSet<uint>()));
        Assert.Equal(key, row.Key);
        Assert.Equal(0, row.Quantity);
        Assert.True(row.Selected);
        Assert.True(row.Saved);
    }

    [Fact]
    public void ConfigurationRoundTripKeepsCharactersQualityAndLanguageSeparate()
    {
        WithPath(path =>
        {
            var config = ConfigurationFile.Load(path, UiLanguage.Japanese);
            Assert.Equal(UiLanguage.Japanese, config.Language);
            var first = config.ForCharacter(42);
            first.RememberedItems.UnionWith([new(100, ItemQuality.Normal), new(100, ItemQuality.High)]);
            config.ForCharacter(43).RememberNewItems = false;
            config.Language = UiLanguage.English;
            config.SelectionKey = SelectionKey.Alt;
            ConfigurationFile.Save(path, config);
            var restored = ConfigurationFile.Load(path, UiLanguage.Japanese);
            Assert.Equal(UiLanguage.English, restored.Language);
            Assert.Equal(SelectionKey.Alt, restored.SelectionKey);
            Assert.Equal(2, restored.ForCharacter(42).RememberedItems.Count);
            Assert.True(restored.ForCharacter(42).RememberNewItems);
            Assert.Empty(restored.ForCharacter(43).RememberedItems);
            Assert.False(restored.ForCharacter(43).RememberNewItems);
            Assert.Empty(restored.ForCharacter(44).RememberedItems);
            Assert.False(File.Exists(path + ".tmp"));
        });
    }

    [Theory]
    [InlineData("{broken")]
    [InlineData("null")]
    [InlineData("{\"Version\":99}")]
    [InlineData("{\"Language\":\"Unsupported\"}")]
    [InlineData("{\"Characters\":{\"0\":{}}}")]
    [InlineData("{\"Characters\":{\"42\":{\"RememberedItems\":[{\"ItemId\":0,\"Quality\":\"Normal\"}]}}}")]
    public void InvalidConfigurationIsReportedWithoutOverwritingTheOriginal(string json)
    {
        WithPath(path =>
        {
            File.WriteAllText(path, json);
            Assert.ThrowsAny<Exception>(() => ConfigurationFile.Load(path, UiLanguage.English));
            Assert.Equal(json, File.ReadAllText(path));
        });
    }

    [Fact]
    public void SaveFailureIsReportedAndTheOriginalFileRemainsReadable()
    {
        WithPath(path =>
        {
            var config = new Configuration();
            ConfigurationFile.Save(path, config);
            var original = File.ReadAllText(path);
            using var lockFile = new FileStream(path + ".tmp", FileMode.Create, FileAccess.ReadWrite, FileShare.None);
            config.Language = UiLanguage.Japanese;
            Assert.ThrowsAny<IOException>(() => ConfigurationFile.Save(path, config));
            Assert.Equal(original, File.ReadAllText(path));
        });
    }

    [Fact]
    public void EveryMessageFormatsInBothLanguagesWithEnglishFallback()
    {
        foreach (var id in Enum.GetValues<TextId>())
        {
            var english = Localization.Get(UiLanguage.English, id, 1, 2, 3);
            var japanese = Localization.Get(UiLanguage.Japanese, id, 1, 2, 3);
            Assert.False(string.IsNullOrWhiteSpace(english));
            Assert.False(string.IsNullOrWhiteSpace(japanese));
            Assert.Equal(english, Localization.Get((UiLanguage)999, id, 1, 2, 3));
            if (id is not (TextId.NormalQuality or TextId.HighQuality))
                Assert.NotEqual(english, japanese);
        }
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
