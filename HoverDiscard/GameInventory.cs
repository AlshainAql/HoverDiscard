using System.Runtime.InteropServices;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Keys;
using FFXIVClientStructs.FFXIV.Client.Enums;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using HoverDiscard.Core;
using Lumina.Excel.Sheets;

namespace HoverDiscard;

internal sealed unsafe class GameInventory
{
    private const int SlotsPerPage = 35;
    private readonly Dictionary<uint, ItemInfo> items = [];
    private static readonly uint ProcessId = (uint)Environment.ProcessId;
    private static readonly ConditionFlag[] BusyConditions =
    [
        ConditionFlag.InCombat, ConditionFlag.Unconscious, ConditionFlag.Casting,
        ConditionFlag.Casting87, ConditionFlag.BetweenAreas, ConditionFlag.BetweenAreas51,
        ConditionFlag.Occupied, ConditionFlag.Occupied30, ConditionFlag.Occupied33,
        ConditionFlag.Occupied38, ConditionFlag.Occupied39, ConditionFlag.OccupiedInEvent,
        ConditionFlag.OccupiedInQuestEvent, ConditionFlag.OccupiedInCutSceneEvent,
        ConditionFlag.OccupiedSummoningBell, ConditionFlag.WatchingCutscene,
        ConditionFlag.WatchingCutscene78, ConditionFlag.TradeOpen,
        ConditionFlag.Crafting, ConditionFlag.Gathering,
    ];

    public static bool HasFocus
    {
        get
        {
            var window = GetForegroundWindow();
            return window != 0 && GetWindowThreadProcessId(window, out var processId) != 0
                && processId == ProcessId;
        }
    }

    public bool IsSafe => Services.ClientState.IsLoggedIn && HasFocus
        && !BusyConditions.Any(flag => Services.Condition[flag])
        && !IsVisible("SelectYesno") && !IsVisible("InputNumeric");

    private static bool IsVisible(string name)
    {
        var addon = Services.GameGui.GetAddonByName(name);
        return addon != 0 && ((AtkUnitBase*)addon.Address)->IsVisible;
    }

    public ItemInfo GetItem(uint id)
    {
        if (items.TryGetValue(id, out var cached))
            return cached;
        var row = Services.Data.GetExcelSheet<Item>()?.GetRowOrDefault(id);
        var info = row is { } item
            ? new ItemInfo(item.Name.ToString(), item.Icon, !item.IsIndisposable)
            : new ItemInfo("", 0, false);
        items[id] = info;
        return info;
    }

    public InventorySnapshot? Read()
    {
        var contentId = Services.PlayerState.ContentId;
        if (!Services.ClientState.IsLoggedIn || contentId == 0)
            return null;
        var manager = InventoryManager.Instance();
        if (manager == null)
            return null;
        var result = new SlotState[4 * SlotsPerPage];
        for (var page = 0; page < 4; page++)
        {
            var container = manager->GetInventoryContainer((InventoryType)page);
            if (container == null || !container->IsLoaded || container->Size != SlotsPerPage)
                return null;
            for (var slot = 0; slot < SlotsPerPage; slot++)
            {
                var item = container->GetInventorySlot(slot);
                if (item == null || item->IsSymbolic)
                    return null;
                var address = new SlotAddress(page, slot);
                if (item->ItemId == 0 || item->Quantity <= 0)
                {
                    result[page * SlotsPerPage + slot] = SlotState.Empty(address);
                    continue;
                }
                var quality = item->Flags.HasFlag(InventoryItem.ItemFlags.Collectable)
                    ? ItemQuality.Collectable
                    : item->Flags.HasFlag(InventoryItem.ItemFlags.HighQuality) ? ItemQuality.High : ItemQuality.Normal;
                // Stable item attributes catch swaps, sorting, dyes, materia and gear changes.
                var fingerprint = $"{(byte)item->Flags}:{item->Condition}:{item->SpiritbondOrCollectability}:"
                    + $"{item->CrafterContentId}:{item->GlamourId}:"
                    + string.Join(',', item->Materia.ToArray()) + ":"
                    + string.Join(',', item->MateriaGrades.ToArray()) + ":"
                    + string.Join(',', item->Stains.ToArray());
                result[page * SlotsPerPage + slot] = new SlotState(address,
                    new ItemKey(item->ItemId, quality), item->Quantity, fingerprint, GetItem(item->ItemId).CanDiscard);
            }
        }
        return new InventorySnapshot(contentId, Services.ClientState.TerritoryType, result);
    }

    public static bool SelectionKeyDown(SelectionKey key) => Services.KeyState[key switch
    {
        SelectionKey.Ctrl => VirtualKey.CONTROL,
        SelectionKey.Alt => VirtualKey.MENU,
        _ => VirtualKey.SHIFT,
    }];

    public static ItemKey? HoveredInventoryItem(InventorySnapshot inventory)
    {
        var hovered = Services.GameGui.HoveredItem;
        if (hovered == 0)
            return null;
        var detail = AgentItemDetail.Instance();
        if (detail == null || !detail->IsAgentActive() || detail->DetailKind != DetailKind.InventoryItem
            || detail->TypeOrId is < 48 or > 51 || detail->Index >= SlotsPerPage)
            return null;

        var stage = AtkStage.Instance();
        if (stage == null || stage->AtkCollisionManager == null)
            return null;
        var addon = stage->AtkCollisionManager->IntersectingAddon;
        if (addon == null)
            return null;
        return HoverSelection.Resolve(inventory, true, detail->TypeOrId, detail->Index,
            hovered, detail->ItemId, addon->NameString);
    }

    public static void Discard(SlotAddress address)
    {
        var manager = InventoryManager.Instance();
        if (manager == null)
            throw new InvalidOperationException("Inventory manager is unavailable.");
        // The native request has no documented success return. Confirm the slot change instead.
        try
        {
            manager->DiscardItem((InventoryType)address.Page, (ushort)address.Slot);
        }
        catch (Exception exception)
        {
            Services.Log.Error(exception, "Could not send a discard request for page {Page}, slot {Slot}.",
                address.Page, address.Slot);
            throw;
        }
    }

    [DllImport("user32.dll")] private static extern nint GetForegroundWindow();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(nint window, out uint processId);
}

internal sealed record ItemInfo(string Name, uint Icon, bool CanDiscard);
