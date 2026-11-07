using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Interface.ManagedFontAtlas;
using Dalamud.Interface.Utility;

namespace HoverDiscard;

internal sealed class InventoryButton(Plugin plugin)
{
    private bool loggedAnchorError;

    public void Draw(IFontHandle font)
    {
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
                var discard = ImGuiComponents.IconButton("InventoryDiscard", FontAwesomeIcon.Trash, new Vector2(size));
                if (ImGui.IsItemClicked(ImGuiMouseButton.Right))
                    plugin.OpenWindow();
                else if (discard)
                    plugin.RequestRememberedDiscard();
            }
            finally { ImGui.End(); }
        }
        finally { ImGui.PopStyleVar(); }
    }
}
