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
    private static readonly Vector4 WarningColor = new(1f, 0.75f, 0.35f, 1f);

    public MainWindow(Plugin plugin) : base("HoverDiscard###HoverDiscardMain")
    {
        this.plugin = plugin;
        Size = new Vector2(820, 560);
        SizeCondition = ImGuiCond.FirstUseEver;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(680, 440),
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
            plugin.LastSelectionDraw = plugin.Now;
            plugin.AllowHover = !ImGui.GetIO().WantTextInput && !ImGui.IsAnyItemActive();
            DrawDiscardList();
            ImGui.EndTabItem();
        }
        if (ImGui.BeginTabItem(plugin.T(TextId.RememberedItems) + "###RememberedTab"))
        {
            DrawRememberedItems();
            ImGui.EndTabItem();
        }
        if (ImGui.BeginTabItem(plugin.T(TextId.Settings) + "###SettingsTab"))
        {
            DrawSettings();
            ImGui.EndTabItem();
        }
        ImGui.EndTabBar();
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

        var rows = plugin.Selection.Rows(plugin.Inventory, character.RememberedItems)
            .Where(r => r.Quantity > 0 || plugin.Selection.NewItems.Contains(r.Key))
            .OrderBy(r => plugin.Game.GetItem(r.Key.ItemId).Name).ThenBy(r => r.Key.Quality).ToArray();
        var selected = rows.Where(r => r.Selected && r.CanDiscard).ToArray();
        ImGui.TextUnformatted(plugin.T(TextId.SelectedTotal,
            selected.Length, selected.Sum(r => r.Quantity), selected.Sum(r => r.Stacks)));
        var height = Math.Max(120, ImGui.GetContentRegionAvail().Y - 145);
        if (ImGui.BeginTable("###DiscardTable", 6,
            ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerH | ImGuiTableFlags.ScrollY
            | ImGuiTableFlags.Resizable, new Vector2(0, height)))
        {
            Column(TextId.Use, ImGuiTableColumnFlags.WidthFixed, 45);
            Column(TextId.Item, ImGuiTableColumnFlags.WidthStretch);
            Column(TextId.Quality, ImGuiTableColumnFlags.WidthFixed, 65);
            Column(TextId.Quantity, ImGuiTableColumnFlags.WidthFixed, 60);
            Column(TextId.Stacks, ImGuiTableColumnFlags.WidthFixed, 45);
            Column(TextId.Source, ImGuiTableColumnFlags.WidthFixed, 90);
            ImGui.TableSetupScrollFreeze(0, 1);
            ImGui.TableHeadersRow();
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
                DrawItem(row.Key);
                if (!row.CanDiscard && row.Quantity > 0)
                    Tooltip(TextId.CannotDiscard);
                ImGui.TableNextColumn();
                ImGui.TextUnformatted(Quality(row.Key));
                ImGui.TableNextColumn();
                ImGui.TextUnformatted(row.Quantity.ToString());
                ImGui.TableNextColumn();
                ImGui.TextUnformatted(row.Stacks.ToString());
                ImGui.TableNextColumn();
                ImGui.TextUnformatted(plugin.T(row.Saved ? TextId.Saved : TextId.New));
                ImGui.PopID();
            }
            ImGui.EndTable();
        }
        if (rows.Length == 0)
            ImGui.TextDisabled(plugin.T(TextId.EmptyList));
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
            plugin.Start();
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
        if (character.RememberedItems.Count == 0)
        {
            ImGui.TextWrapped(plugin.T(TextId.EmptyRules));
            return;
        }
        if (!ImGui.BeginTable("###RememberedTable", 5,
            ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerH | ImGuiTableFlags.ScrollY
            | ImGuiTableFlags.Resizable, new Vector2(0, ImGui.GetContentRegionAvail().Y)))
            return;
        Column(TextId.Item, ImGuiTableColumnFlags.WidthStretch);
        Column(TextId.Quality, ImGuiTableColumnFlags.WidthFixed, 65);
        Column(TextId.Quantity, ImGuiTableColumnFlags.WidthFixed, 60);
        Column(TextId.Stacks, ImGuiTableColumnFlags.WidthFixed, 45);
        ImGui.TableSetupColumn("##Action", ImGuiTableColumnFlags.WidthFixed, 100);
        ImGui.TableSetupScrollFreeze(0, 1);
        ImGui.TableHeadersRow();
        foreach (var row in plugin.Selection.Rows(plugin.Inventory, character.RememberedItems)
            .Where(r => r.Saved).OrderBy(r => plugin.Game.GetItem(r.Key.ItemId).Name).ThenBy(r => r.Key.Quality))
        {
            ImGui.PushID($"{row.Key.ItemId}:{row.Key.Quality}");
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            DrawItem(row.Key);
            ImGui.TableNextColumn();
            ImGui.TextUnformatted(Quality(row.Key));
            ImGui.TableNextColumn();
            ImGui.TextUnformatted(row.Quantity.ToString());
            ImGui.TableNextColumn();
            ImGui.TextUnformatted(row.Stacks.ToString());
            ImGui.TableNextColumn();
            ImGui.BeginDisabled(plugin.Running || plugin.ConfigurationInvalid);
            if (ImGui.Button(plugin.T(TextId.Remove)))
                plugin.Forget(row.Key);
            ImGui.EndDisabled();
            ImGui.PopID();
        }
        ImGui.EndTable();
    }

    private void DrawSettings()
    {
        var language = (int)plugin.Config.Language;
        if (ImGui.Combo(plugin.T(TextId.Language) + "###Language", ref language, "English\0日本語\0"))
        {
            plugin.ChangeLanguage((UiLanguage)language);
        }
        ImGui.BeginDisabled(plugin.Running || plugin.ConfigurationInvalid);
        var key = (int)plugin.Config.SelectionKey;
        if (ImGui.Combo(plugin.T(TextId.SelectionKey) + "###SelectionKey", ref key, "Shift\0Ctrl\0Alt\0"))
        {
            plugin.Config.SelectionKey = (SelectionKey)key;
            plugin.Save();
        }
        ImGui.EndDisabled();
        ImGui.TextWrapped(plugin.T(TextId.SelectionKeyHint));
    }

    private void DrawItem(ItemKey key)
    {
        var info = plugin.Game.GetItem(key.ItemId);
        if (info.Icon != 0)
        {
            var texture = Services.Textures.GetFromGameIcon(new GameIconLookup(info.Icon)).GetWrapOrDefault();
            if (texture != null)
            {
                ImGui.Image(texture.Handle, new Vector2(24));
                ImGui.SameLine();
            }
        }
        ImGui.TextUnformatted(string.IsNullOrEmpty(info.Name) ? plugin.T(TextId.MissingItem, key.ItemId) : info.Name);
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
