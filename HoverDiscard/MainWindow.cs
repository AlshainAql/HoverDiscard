using System.Numerics;
using Dalamud;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.ManagedFontAtlas;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Windowing;
using HoverDiscard.Core;

namespace HoverDiscard;

internal sealed class MainWindow : Window, IDisposable
{
    private readonly Plugin plugin;
    private readonly IFontHandle font;
    internal IFontHandle Font => font;
    private readonly ListFilter discardFilter = new();
    private readonly ListFilter rememberedFilter = new();
    private readonly ListFilter whitelistFilter = new();
    private static readonly Vector4 WarningColor = new(1f, 0.75f, 0.35f, 1f);

    private sealed class ListFilter
    {
        public uint? Category;
        public string Search = "";
    }

    public MainWindow(Plugin plugin) : base("HoverDiscard###HoverDiscardMain")
    {
        this.plugin = plugin;
        Size = new Vector2(920, 640);
        SizeCondition = ImGuiCond.FirstUseEver;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(720, 520),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue),
        };
        font = Services.PluginInterface.UiBuilder.FontAtlas.NewDelegateFontHandle(toolkit =>
            toolkit.OnPreBuild(build => build.Font = build.AddDalamudAssetFont(DalamudAsset.NotoSansCjkRegular,
                new SafeFontConfig
                {
                    SizePx = 17,
                    GlyphRanges = [0x20, 0x024F, 0x2000, 0x206F, 0x3000, 0x30FF, 0x4E00, 0x9FFF, 0xFF00, 0xFFEF, 0],
                })));
    }

    public override void Draw()
    {
        using var fontScope = font.Push();
        plugin.AllowHover = false;
        plugin.HoverToWhitelist = false;
        if (plugin.ConfigurationInvalid)
            Warning(TextId.ConfigInvalid);
        if (plugin.SaveFailed)
        {
            Warning(TextId.SaveFailed);
            if (ImGui.Button(plugin.T(TextId.RetrySave)))
                plugin.Save();
        }
        if (plugin.RuntimeError)
            Warning(TextId.RuntimeError);

        // Stable IDs keep the selected tab and table state when changing language.
        if (!ImGui.BeginTabBar("###Tabs"))
            return;
        if (ImGui.BeginTabItem(plugin.T(TextId.DiscardList) + "###DiscardTab"))
        {
            DrawDiscardList();
            EnableHover(false);
            ImGui.EndTabItem();
        }
        if (ImGui.BeginTabItem(plugin.T(TextId.RememberedItems) + "###RememberedTab"))
        {
            DrawRememberedItems();
            ImGui.EndTabItem();
        }
        if (ImGui.BeginTabItem(plugin.T(TextId.Whitelist) + "###WhitelistTab"))
        {
            DrawWhitelist();
            EnableHover(true);
            ImGui.EndTabItem();
        }
        if (ImGui.BeginTabItem(plugin.T(TextId.Settings) + "###SettingsTab"))
        {
            DrawSettings();
            ImGui.EndTabItem();
        }
        ImGui.EndTabBar();
    }

    private void EnableHover(bool whitelist)
    {
        plugin.LastSelectionDraw = plugin.Now;
        plugin.HoverToWhitelist = whitelist;
        plugin.AllowHover = !ImGui.GetIO().WantTextInput && !ImGui.IsAnyItemActive();
    }

    private void DrawDiscardList()
    {
        ImGui.TextWrapped(plugin.T(TextId.Instructions, plugin.Config.SelectionKey.ToString()));
        ImGui.TextDisabled(plugin.T(TextId.ExcludedHint));
        var character = plugin.Character;
        if (character == null)
        {
            ImGui.TextWrapped(plugin.T(TextId.Login));
            return;
        }
        ImGui.BeginDisabled(plugin.Running || plugin.ConfigurationInvalid);
        var remember = character.RememberNewItems;
        if (ImGui.Checkbox(plugin.T(TextId.RememberNew), ref remember))
        {
            character.RememberNewItems = remember;
            plugin.Save();
        }
        Tooltip(TextId.RememberHint);
        ImGui.EndDisabled();

        var allRows = plugin.Selection.Rows(plugin.Inventory, character.RememberedItems, character.WhitelistedItems)
            .Where(r => !character.WhitelistedItems.Contains(r.Key.ItemId)
                && (r.Quantity > 0 || plugin.Selection.NewItems.Contains(r.Key))).ToArray();
        var visible = FilterItems("DiscardFilter", allRows.Select(r => r.Key.ItemId), discardFilter);
        var rows = SortRows(allRows.Where(r => visible.Contains(r.Key.ItemId)));
        var selected = rows.Where(r => r.Selected && r.CanDiscard).ToArray();
        ImGui.TextUnformatted(plugin.T(TextId.SelectedTotal,
            selected.Length, selected.Sum(r => r.Quantity), selected.Sum(r => r.Stacks)));
        ImGui.TextWrapped(plugin.T(TextId.FilteredDiscardHint));
        var height = Math.Max(90, ImGui.GetContentRegionAvail().Y - 145);
        if (ImGui.BeginTable("###DiscardTable", 7, TableFlags, new Vector2(0, height)))
        {
            Column(TextId.Use, ImGuiTableColumnFlags.WidthFixed, 40);
            Column(TextId.Item, ImGuiTableColumnFlags.WidthStretch);
            Column(TextId.Quality, ImGuiTableColumnFlags.WidthFixed, 60);
            Column(TextId.Quantity, ImGuiTableColumnFlags.WidthFixed, 55);
            Column(TextId.Stacks, ImGuiTableColumnFlags.WidthFixed, 45);
            Column(TextId.Source, ImGuiTableColumnFlags.WidthFixed, 80);
            ImGui.TableSetupColumn("##Action", ImGuiTableColumnFlags.WidthFixed, 80);
            Headers();
            foreach (var row in rows)
            {
                ImGui.PushID($"{row.Key.ItemId}:{row.Key.Quality}");
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGui.BeginDisabled(plugin.Running || !row.CanDiscard || plugin.ConfigurationInvalid);
                var use = row.Selected;
                if (ImGui.Checkbox("##Use", ref use))
                {
                    if (use)
                        plugin.Selection.Excluded.Remove(row.Key);
                    else
                        plugin.Selection.Excluded.Add(row.Key);
                }
                ImGui.EndDisabled();
                ImGui.TableNextColumn();
                DrawItem(row.Key.ItemId);
                if (!row.CanDiscard && row.Quantity > 0)
                    Tooltip(TextId.CannotDiscard);
                DrawAmounts(row);
                ImGui.TableNextColumn();
                ImGui.TextUnformatted(plugin.T(row.Saved ? TextId.Saved : TextId.New));
                ImGui.TableNextColumn();
                ProtectButton(row.Key.ItemId);
                ImGui.PopID();
            }
            ImGui.EndTable();
        }
        if (rows.Length == 0)
            ImGui.TextDisabled(plugin.T(allRows.Length == 0 ? TextId.EmptyList : TextId.NoMatches));
        if (plugin.Inventory == null)
            ImGui.TextWrapped(plugin.T(TextId.InventoryUnavailable));

        ImGui.BeginDisabled(plugin.Running || plugin.ConfigurationInvalid);
        if (ImGui.Button(plugin.T(TextId.ClearSelection)))
        {
            plugin.Selection.Excluded.ExceptWith(plugin.Selection.NewItems);
            plugin.Selection.NewItems.Clear();
        }
        Tooltip(TextId.ClearHint);
        ImGui.SameLine();
        ImGui.BeginDisabled(!plugin.Safe || selected.Length == 0);
        if (ImGui.Button(plugin.T(TextId.DiscardAll)))
            plugin.Start(rows.Select(r => r.Key).ToHashSet());
        ImGui.EndDisabled();
        ImGui.EndDisabled();
        if (plugin.Running)
        {
            ImGui.SameLine();
            if (ImGui.Button(plugin.T(TextId.Stop)))
                plugin.Stop();
            ImGui.TextWrapped(plugin.T(TextId.Progress, plugin.Batch!.CompletedStacks, plugin.Batch.TotalStacks));
            ImGui.TextWrapped(plugin.T(plugin.Stopping ? TextId.PendingStop : TextId.ActiveHint));
        }
        else
        {
            if (plugin.Notice is { } notice)
                ImGui.TextWrapped(plugin.T(notice));
            else if (!plugin.Safe && plugin.Inventory != null)
                ImGui.TextWrapped(plugin.T(TextId.Unsafe));
        }
        Warning(TextId.Irreversible);
    }

    private void DrawRememberedItems()
    {
        ImGui.TextWrapped(plugin.T(TextId.RuleHint));
        var character = plugin.Character;
        if (character == null)
        {
            ImGui.TextWrapped(plugin.T(TextId.Login));
            return;
        }
        ImGui.TextUnformatted(plugin.T(TextId.RememberedCount, character.RememberedItems.Count));
        var allRows = plugin.Selection.Rows(plugin.Inventory, character.RememberedItems, character.WhitelistedItems)
            .Where(r => r.Saved).ToArray();
        var visible = FilterItems("RememberedFilter", allRows.Select(r => r.Key.ItemId), rememberedFilter);
        var rows = SortRows(allRows.Where(r => visible.Contains(r.Key.ItemId)));
        if (rows.Length == 0)
        {
            ImGui.TextWrapped(plugin.T(allRows.Length == 0 ? TextId.EmptyRules : TextId.NoMatches));
            return;
        }
        if (!ImGui.BeginTable("###RememberedTable", 6, TableFlags, new Vector2(0, ImGui.GetContentRegionAvail().Y)))
            return;
        Column(TextId.Item, ImGuiTableColumnFlags.WidthStretch);
        Column(TextId.Quality, ImGuiTableColumnFlags.WidthFixed, 60);
        Column(TextId.Quantity, ImGuiTableColumnFlags.WidthFixed, 55);
        Column(TextId.Stacks, ImGuiTableColumnFlags.WidthFixed, 45);
        Column(TextId.Status, ImGuiTableColumnFlags.WidthFixed, 65);
        ImGui.TableSetupColumn("##Action", ImGuiTableColumnFlags.WidthFixed, 190);
        Headers();
        foreach (var row in rows)
        {
            ImGui.PushID($"{row.Key.ItemId}:{row.Key.Quality}");
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            DrawItem(row.Key.ItemId);
            DrawAmounts(row);
            ImGui.TableNextColumn();
            var isProtected = character.WhitelistedItems.Contains(row.Key.ItemId);
            ImGui.TextUnformatted(plugin.T(isProtected ? TextId.Protected : TextId.Saved));
            if (isProtected)
                Tooltip(TextId.ProtectedHint);
            ImGui.TableNextColumn();
            ProtectButton(row.Key.ItemId);
            ImGui.SameLine();
            ImGui.BeginDisabled(plugin.Running || plugin.ConfigurationInvalid);
            if (ImGui.Button(plugin.T(TextId.Remove)))
                plugin.Forget(row.Key);
            ImGui.EndDisabled();
            ImGui.PopID();
        }
        ImGui.EndTable();
    }

    private void DrawWhitelist()
    {
        ImGui.TextWrapped(plugin.T(TextId.WhitelistInstructions, plugin.Config.SelectionKey.ToString()));
        ImGui.TextWrapped(plugin.T(TextId.WhitelistHint));
        var character = plugin.Character;
        if (character == null)
        {
            ImGui.TextWrapped(plugin.T(TextId.Login));
            return;
        }
        ImGui.TextUnformatted(plugin.T(TextId.WhitelistCount, character.WhitelistedItems.Count));
        var visible = FilterItems("WhitelistFilter", character.WhitelistedItems, whitelistFilter);
        var ids = visible.OrderBy(id => plugin.Game.GetItem(id).Name).ThenBy(id => id).ToArray();
        if (ids.Length == 0)
        {
            ImGui.TextWrapped(plugin.T(character.WhitelistedItems.Count == 0 ? TextId.EmptyWhitelist : TextId.NoMatches));
            return;
        }
        if (!ImGui.BeginTable("###WhitelistTable", 4, TableFlags, new Vector2(0, ImGui.GetContentRegionAvail().Y)))
            return;
        Column(TextId.Item, ImGuiTableColumnFlags.WidthStretch);
        Column(TextId.Quantity, ImGuiTableColumnFlags.WidthFixed, 70);
        Column(TextId.Stacks, ImGuiTableColumnFlags.WidthFixed, 55);
        ImGui.TableSetupColumn("##Action", ImGuiTableColumnFlags.WidthFixed, 110);
        Headers();
        foreach (var id in ids)
        {
            ImGui.PushID(id.ToString());
            var slots = plugin.Inventory?.Slots.Where(s => s.Key?.ItemId == id).ToArray() ?? [];
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            DrawItem(id);
            ImGui.TableNextColumn();
            ImGui.TextUnformatted(slots.Sum(s => s.Quantity).ToString());
            ImGui.TableNextColumn();
            ImGui.TextUnformatted(slots.Length.ToString());
            ImGui.TableNextColumn();
            ImGui.BeginDisabled(plugin.Running || plugin.ConfigurationInvalid);
            if (ImGui.Button(plugin.T(TextId.Unprotect)))
                plugin.Unprotect(id);
            Tooltip(TextId.UnprotectHint);
            ImGui.EndDisabled();
            ImGui.PopID();
        }
        ImGui.EndTable();
    }

    private HashSet<uint> FilterItems(string id, IEnumerable<uint> itemIds, ListFilter filter)
    {
        var ids = itemIds.Distinct().ToArray();
        var categories = ids.Select(itemId => plugin.Game.GetItem(itemId).CategoryId).Distinct()
            .OrderBy(category => plugin.Game.GetCategory(category, plugin.Config.Language)).ToArray();
        var reset = filter.Category.HasValue && !categories.Contains(filter.Category.Value);
        if (reset)
            filter.Category = null;
        ImGui.PushID(id);
        ImGui.SetNextItemWidth(-1);
        ImGui.InputTextWithHint("##Search", plugin.T(TextId.SearchHint), ref filter.Search, 256);
        if (ImGui.BeginTabBar("##Categories", ImGuiTabBarFlags.FittingPolicyScroll))
        {
            if (ImGui.BeginTabItem(plugin.T(TextId.AllCategories) + "###All",
                reset ? ImGuiTabItemFlags.SetSelected : ImGuiTabItemFlags.None))
            {
                filter.Category = null;
                ImGui.EndTabItem();
            }
            foreach (var category in categories)
            {
                if (ImGui.BeginTabItem(plugin.Game.GetCategory(category, plugin.Config.Language) + $"###Category{category}"))
                {
                    filter.Category = category;
                    ImGui.EndTabItem();
                }
            }
            ImGui.EndTabBar();
        }
        ImGui.PopID();
        var search = filter.Search.Trim();
        return ids.Where(itemId =>
        {
            var item = plugin.Game.GetItem(itemId);
            return (!filter.Category.HasValue || item.CategoryId == filter.Category)
                && (search.Length == 0 || item.Name.Contains(search, StringComparison.OrdinalIgnoreCase)
                    || itemId.ToString().Contains(search, StringComparison.Ordinal));
        }).ToHashSet();
    }

    private ItemRow[] SortRows(IEnumerable<ItemRow> rows) => rows
        .OrderBy(r => plugin.Game.GetItem(r.Key.ItemId).Name)
        .ThenBy(r => r.Key.ItemId).ThenBy(r => r.Key.Quality).ToArray();

    private void ProtectButton(uint itemId)
    {
        ImGui.BeginDisabled(plugin.Running || plugin.ConfigurationInvalid
            || plugin.Character?.WhitelistedItems.Contains(itemId) == true);
        if (ImGui.Button(plugin.T(TextId.Protect)))
            plugin.Protect(itemId);
        Tooltip(TextId.ProtectHint);
        ImGui.EndDisabled();
    }

    private void DrawAmounts(ItemRow row)
    {
        ImGui.TableNextColumn();
        ImGui.TextUnformatted(Quality(row.Key));
        ImGui.TableNextColumn();
        ImGui.TextUnformatted(row.Quantity.ToString());
        ImGui.TableNextColumn();
        ImGui.TextUnformatted(row.Stacks.ToString());
    }

    private static ImGuiTableFlags TableFlags => ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerH
        | ImGuiTableFlags.ScrollY | ImGuiTableFlags.Resizable;

    private static void Headers()
    {
        ImGui.TableSetupScrollFreeze(0, 1);
        ImGui.TableHeadersRow();
    }

    private void DrawSettings()
    {
        var language = (int)plugin.Config.Language;
        if (ImGui.Combo(plugin.T(TextId.Language) + "###Language", ref language, "English\0日本語\0"))
            plugin.ChangeLanguage((UiLanguage)language);
        ImGui.BeginDisabled(plugin.Running || plugin.ConfigurationInvalid);
        var key = (int)plugin.Config.SelectionKey;
        if (ImGui.Combo(plugin.T(TextId.SelectionKey) + "###SelectionKey", ref key, "Shift\0Ctrl\0Alt\0"))
        {
            plugin.Config.SelectionKey = (SelectionKey)key;
            plugin.Save();
        }
        ImGui.EndDisabled();
        ImGui.TextWrapped(plugin.T(TextId.SelectionKeyHint));
        ImGui.Separator();
        ImGui.BeginDisabled(plugin.ConfigurationInvalid);
        var showButton = plugin.Config.ShowInventoryButton;
        if (ImGui.Checkbox(plugin.T(TextId.ShowInventoryButton), ref showButton))
        {
            plugin.Config.ShowInventoryButton = showButton;
            plugin.Save();
        }
        ImGui.EndDisabled();
        ImGui.TextWrapped(plugin.T(TextId.InventoryButtonHint));
        ImGui.Separator();
        ImGui.TextWrapped(plugin.T(TextId.CommandHelp));
    }

    private void DrawItem(uint itemId)
    {
        var info = plugin.Game.GetItem(itemId);
        if (info.Icon != 0)
        {
            var texture = Services.Textures.GetFromGameIcon(new GameIconLookup(info.Icon)).GetWrapOrDefault();
            if (texture != null)
            {
                ImGui.Image(texture.Handle, new Vector2(24));
                ImGui.SameLine();
            }
        }
        ImGui.TextUnformatted(string.IsNullOrEmpty(info.Name) ? plugin.T(TextId.MissingItem, itemId) : info.Name);
    }

    private string Quality(ItemKey key) => plugin.T(key.Quality switch
    {
        ItemQuality.High => TextId.HighQuality,
        ItemQuality.Collectable => TextId.Collectable,
        _ => TextId.NormalQuality,
    });

    private void Column(TextId id, ImGuiTableColumnFlags flags, float width = 0) =>
        ImGui.TableSetupColumn(plugin.T(id), flags, width);

    private void Warning(TextId id)
    {
        ImGui.PushStyleColor(ImGuiCol.Text, WarningColor);
        ImGui.TextWrapped(plugin.T(id));
        ImGui.PopStyleColor();
    }

    private void Tooltip(TextId id)
    {
        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
        {
            ImGui.BeginTooltip();
            ImGui.PushTextWrapPos(ImGui.GetFontSize() * 32);
            ImGui.TextUnformatted(plugin.T(id));
            ImGui.PopTextWrapPos();
            ImGui.EndTooltip();
        }
    }

    public override void OnClose()
    {
        plugin.AllowHover = false;
        plugin.Stop();
    }

    public void Dispose() => font.Dispose();
}
