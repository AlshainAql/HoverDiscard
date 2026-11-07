using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Interface.ManagedFontAtlas;
using Dalamud.Interface.Utility;
using HoverDiscard.Core;

namespace HoverDiscard;

internal sealed class InventoryButton(Plugin plugin)
{
    private bool loggedAnchorError;
    public bool Visible { get; private set; }

    public void Draw(IFontHandle font)
    {
        Visible = false;
        if (!plugin.Config.ShowInventoryButton)
            return;
        Vector2? anchor;
        try
        {
            anchor = GameInventory.InventoryAnchor();
            loggedAnchorError = false;
        }
        catch (Exception exception)
        {
            if (!loggedAnchorError)
                Services.Log.Error(exception, "Could not locate the inventory window.");
            loggedAnchorError = true;
            return;
        }
        if (anchor == null)
            return;
        Visible = true;

        using var fontScope = font.Push();
        var viewport = ImGui.GetMainViewport();
        var size = Math.Max(28 * ImGuiHelpers.GlobalScale, ImGui.GetFrameHeight());
        var position = viewport.Pos + anchor.Value - new Vector2(size + 4 * ImGuiHelpers.GlobalScale, 0);
        var maximum = Vector2.Max(viewport.WorkPos, viewport.WorkPos + viewport.WorkSize - new Vector2(size));
        position = Vector2.Clamp(position, viewport.WorkPos, maximum);
        ImGui.SetNextWindowPos(position, ImGuiCond.Always);
        ImGui.SetNextWindowSize(new Vector2(size), ImGuiCond.Always);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
        try
        {
            var visible = ImGui.Begin("###HoverDiscardInventoryButton", ImGuiWindowFlags.NoDecoration
                | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoBackground
                | ImGuiWindowFlags.NoFocusOnAppearing | ImGuiWindowFlags.NoNav);
            try
            {
                if (!visible)
                    return;
                if (ImGuiComponents.IconButton("InventoryMenu", FontAwesomeIcon.Trash, new Vector2(size)))
                    ImGui.OpenPopup("###InventoryActions");
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip(plugin.T(TextId.InventoryButtonTooltip));
                // Restore normal padding for the menu, which is a separate ImGui window.
                ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(8 * ImGuiHelpers.GlobalScale));
                try
                {
                    if (ImGui.BeginPopup("###InventoryActions"))
                    {
                        try { DrawMenu(); }
                        finally { ImGui.EndPopup(); }
                    }
                }
                finally { ImGui.PopStyleVar(); }
            }
            finally { ImGui.End(); }
        }
        finally { ImGui.PopStyleVar(); }
    }

    private void DrawMenu()
    {
        ImGui.TextUnformatted("HoverDiscard");
        ImGui.Separator();
        var settings = plugin.Character;
        var targets = settings == null ? new HashSet<ItemKey>()
            : RememberedDiscardRequest.Targets(settings, plugin.Selection.Excluded);
        var slots = plugin.Inventory?.Slots.Where(slot => slot.Key is { } key && targets.Contains(key)
            && slot.CanDiscard && slot.Quantity > 0).ToArray() ?? [];
        ImGui.TextUnformatted(plugin.T(TextId.RememberedTargetTotal,
            slots.Select(s => s.Key).Distinct().Count(), slots.Sum(s => s.Quantity), slots.Length));
        ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + 340 * ImGuiHelpers.GlobalScale);
        ImGui.TextWrapped(plugin.T(TextId.CommandScopeHint));
        if (plugin.Running)
            ImGui.TextWrapped(plugin.T(TextId.Progress, plugin.Batch!.CompletedStacks, plugin.Batch.TotalStacks));
        else if (plugin.ConfigurationInvalid || plugin.SaveFailed)
            ImGui.TextWrapped(plugin.T(plugin.ConfigurationInvalid ? TextId.ConfigInvalid : TextId.SaveFailed));
        else if (plugin.Inventory == null)
            ImGui.TextWrapped(plugin.T(TextId.InventoryUnavailable));
        else if (!plugin.Safe)
            ImGui.TextWrapped(plugin.T(TextId.Unsafe));
        ImGui.PopTextWrapPos();

        ImGui.BeginDisabled(plugin.Running || plugin.Starting || !plugin.Safe || slots.Length == 0);
        if (ImGui.MenuItem(plugin.T(TextId.DiscardRemembered)))
            plugin.RequestRememberedDiscard();
        ImGui.EndDisabled();
        ImGui.BeginDisabled(!plugin.Running && !plugin.Starting);
        if (ImGui.MenuItem(plugin.T(TextId.Stop)))
            plugin.StopWithFeedback();
        ImGui.EndDisabled();
        if (ImGui.MenuItem(plugin.T(TextId.OpenPlugin)))
            plugin.OpenWindow();
    }
}
