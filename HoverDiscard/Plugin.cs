using System.Diagnostics;
using Dalamud.Game;
using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using HoverDiscard.Core;

namespace HoverDiscard;

public sealed class Plugin : IDalamudPlugin
{
    private readonly WindowSystem windows = new("HoverDiscard");
    private readonly Stopwatch clock = Stopwatch.StartNew();
    private readonly MainWindow window;
    private readonly InventoryButton inventoryButton;
    private RememberedDiscardRequest? pendingDiscard;
    private bool reportBatchToChat;
    private readonly string configPath;
    private readonly CommandInfo command;
    private ulong contentId;
    private TimeSpan lastUpdate;
    private bool disposed;
    private bool loggedReadError;

    internal Configuration Config { get; }
    internal Selection Selection { get; } = new();
    internal GameInventory Game { get; } = new();
    internal CharacterSettings? Character { get; private set; }
    internal InventorySnapshot? Inventory { get; private set; }
    internal DiscardBatch? Batch { get; private set; }
    internal bool ConfigurationInvalid { get; }
    internal bool SaveFailed { get; private set; }
    internal bool RuntimeError { get; private set; }
    internal bool Safe { get; private set; }
    internal bool Stopping { get; private set; }
    internal bool Running => Batch?.IsRunning == true;
    internal bool Starting => pendingDiscard != null;
    internal TimeSpan Now => clock.Elapsed;
    internal TimeSpan LastSelectionDraw { get; set; } = TimeSpan.MinValue;
    internal bool AllowHover { get; set; }
    internal bool HoverToWhitelist { get; set; }
    internal TextId? Notice { get; private set; }

    public Plugin(IDalamudPluginInterface pluginInterface)
    {
        pluginInterface.Create<Services>();
        configPath = pluginInterface.ConfigFile.FullName;
        var initialLanguage = Services.ClientState.ClientLanguage == ClientLanguage.Japanese
            ? UiLanguage.Japanese : UiLanguage.English;
        try
        {
            Config = ConfigurationFile.Load(configPath, initialLanguage);
        }
        catch (Exception exception)
        {
            Config = new Configuration { Language = initialLanguage };
            ConfigurationInvalid = true;
            Services.Log.Error(exception, "Could not read configuration; discarding and persistence are disabled.");
        }
        window = new MainWindow(this);
        inventoryButton = new InventoryButton(this);
        windows.AddWindow(window);
        command = new CommandInfo(OnCommand) { HelpMessage = T(TextId.CommandHelp) };
        Services.Commands.AddHandler("/hoverdiscard", command);
        pluginInterface.UiBuilder.Draw += Draw;
        pluginInterface.UiBuilder.OpenMainUi += OpenWindow;
        pluginInterface.UiBuilder.OpenConfigUi += OpenWindow;
        Services.Framework.Update += Update;
        if (!ConfigurationInvalid)
            Save();
    }

    internal string T(TextId id, params object[] args) => Localization.Get(Config.Language, id, args);
    internal void OpenWindow() => window.IsOpen = true;
    private void OnCommand(string command, string arguments)
    {
        _ = Services.Framework.RunOnFrameworkThread(() =>
        {
            if (disposed)
                return;
            switch (CommandInput.Parse(arguments))
            {
                case PluginCommand.Open:
                    OpenWindow();
                    break;
                case PluginCommand.Discard:
                    QueueRememberedDiscard();
                    break;
                case PluginCommand.Stop:
                    StopWithFeedback();
                    break;
                default:
                    Reply(TextId.CommandHelp);
                    break;
            }
        });
    }

    private void Draw()
    {
        inventoryButton.Draw(window.Font);
        windows.Draw();
    }

    private void Reply(TextId text, params object[] args) =>
        Services.Chat.Print(T(text, args), "HoverDiscard");

    internal void RequestRememberedDiscard() =>
        _ = Services.Framework.RunOnFrameworkThread(QueueRememberedDiscard);

    private void QueueRememberedDiscard()
    {
        if (disposed)
            return;
        if (Running || Starting)
        {
            Reply(TextId.AlreadyRunning);
            return;
        }
        if (ConfigurationInvalid || SaveFailed)
        {
            Reply(ConfigurationInvalid ? TextId.ConfigInvalid : TextId.SaveFailed);
            return;
        }
        if (!Services.ClientState.IsLoggedIn || Services.PlayerState.ContentId == 0)
        {
            Reply(TextId.Login);
            return;
        }
        pendingDiscard = new RememberedDiscardRequest(Services.PlayerState.ContentId,
            Services.ClientState.TerritoryType);
    }

    internal void StopWithFeedback()
    {
        if (disposed)
            return;
        var wasStarting = Starting;
        if (!Running && !wasStarting)
        {
            Reply(TextId.NotRunning);
            return;
        }
        reportBatchToChat = Running;
        Stop();
        Reply(wasStarting ? TextId.CommandCancelled : TextId.PendingStop);
    }

    private void ProcessRememberedDiscard()
    {
        var request = pendingDiscard;
        pendingDiscard = null;
        if (request == null)
            return;
        TextId? error;
        if (ConfigurationInvalid || SaveFailed)
            error = ConfigurationInvalid ? TextId.ConfigInvalid : TextId.SaveFailed;
        else if (Character == null || contentId != request.ContentId
            || Services.ClientState.TerritoryType != request.TerritoryId)
            error = TextId.ContextChanged;
        else
        {
            Batch = request.Prepare(Inventory, Character, Selection.Excluded, Safe,
                key => Selection.NewItems.Remove(key), out error);
            if (Batch != null)
            {
                Stopping = false;
                Notice = null;
                reportBatchToChat = true;
                Reply(TextId.CommandStarted, Batch.TotalStacks);
                return;
            }
        }
        Notice = error;
        if (error is { } failure)
            Reply(failure);
    }

    private void TickBatch(InventorySnapshot? inventory, bool safe)
    {
        if (!Running)
            return;
        Batch!.Tick(Now, inventory, safe, GameInventory.Discard);
        if (Running)
            return;
        Stopping = false;
        Notice = ResultText(Batch.Result);
        Services.Log.Information("Discard batch ended: {Result}; {Completed}/{Total} stacks.",
            Batch.Result, Batch.CompletedStacks, Batch.TotalStacks);
        if (reportBatchToChat)
        {
            reportBatchToChat = false;
            Reply(Notice.Value);
            Reply(TextId.Progress, Batch.CompletedStacks, Batch.TotalStacks);
        }
    }

    internal void Save()
    {
        if (ConfigurationInvalid)
            return;
        try
        {
            ConfigurationFile.Save(configPath, Config);
            SaveFailed = false;
        }
        catch (Exception exception)
        {
            SaveFailed = true;
            Services.Log.Error(exception, "Could not save HoverDiscard configuration.");
        }
    }

    internal void ChangeLanguage(UiLanguage language)
    {
        Config.Language = language;
        command.HelpMessage = T(TextId.CommandHelp);
        Save();
    }

    internal void Start(IReadOnlySet<ItemKey> visible)
    {
        if (Running || Starting || Inventory == null || Character == null || !Safe || ConfigurationInvalid || SaveFailed)
            return;
        reportBatchToChat = false;
        var settings = Character;
        var included = Selection.Included(settings.RememberedItems, settings.WhitelistedItems, visible);
        var newlySelected = Selection.NewItems.Except(settings.RememberedItems).ToHashSet();
        // Freeze the reviewed snapshot at the button click. The framework revalidates it
        // against fresh inventory data before it is allowed to send any request.
        Batch = new DiscardBatch(Inventory, included, newlySelected, settings.RememberNewItems,
            key =>
            {
                if (!settings.WhitelistedItems.Contains(key.ItemId) && settings.RememberedItems.Add(key))
                    Save();
                if (SaveFailed)
                    Batch?.Stop();
            },
            key => Selection.NewItems.Remove(key), settings.WhitelistedItems);
        Notice = Batch.IsRunning ? null : TextId.NoItems;
        Stopping = false;
    }

    internal void Stop()
    {
        pendingDiscard = null;
        Batch?.Stop();
        Stopping = Running;
    }

    internal void Forget(ItemKey key)
    {
        if (Running || Character == null || ConfigurationInvalid)
            return;
        Character.RememberedItems.Remove(key);
        Selection.Excluded.Remove(key);
        Save();
    }

    internal void Protect(uint itemId)
    {
        if (Running || Character == null || ConfigurationInvalid || itemId == 0)
            return;
        Selection.RemoveItem(itemId);
        if (Character.WhitelistedItems.Add(itemId))
            Save();
    }

    internal void Unprotect(uint itemId)
    {
        if (Running || Character == null || ConfigurationInvalid)
            return;
        if (Character.WhitelistedItems.Remove(itemId))
            Save();
    }

    private void Update(IFramework framework)
    {
        if (disposed || Now - lastUpdate < TimeSpan.FromMilliseconds(50))
            return;
        lastUpdate = Now;
        try
        {
            var currentId = Services.ClientState.IsLoggedIn ? Services.PlayerState.ContentId : 0;
            if (contentId != currentId)
            {
                TickBatch(null, false);
                contentId = currentId;
                Selection.Reset();
                Stopping = false;
                Notice = null;
                Character = currentId == 0 ? null : Config.ForCharacter(currentId);
            }
            if (!window.IsOpen && !Running && !Starting)
            {
                Inventory = null;
                Safe = false;
                return;
            }
            Inventory = Game.Read();
            Safe = Game.IsSafe && !ConfigurationInvalid && !SaveFailed;
            RuntimeError = false;
            loggedReadError = false;

            if (Starting && !Running)
                ProcessRememberedDiscard();
            TickBatch(Inventory, Safe);

            if (!Running && !ConfigurationInvalid && Inventory != null && Character != null && Safe
                && window.IsOpen && AllowHover && Now - LastSelectionDraw < TimeSpan.FromMilliseconds(250)
                && GameInventory.SelectionKeyDown(Config.SelectionKey)
                && GameInventory.HoveredInventoryItem(Inventory, !HoverToWhitelist) is { } key)
            {
                if (HoverToWhitelist)
                    Protect(key.ItemId);
                else
                    Selection.Add(key, Character.RememberedItems, Character.WhitelistedItems);
            }
        }
        catch (Exception exception)
        {
            Inventory = null;
            Safe = false;
            RuntimeError = true;
            if (Starting)
            {
                pendingDiscard = null;
                Reply(TextId.RuntimeError);
            }
            TickBatch(null, false);
            if (!loggedReadError)
                Services.Log.Error(exception, "Could not safely read or update the inventory.");
            loggedReadError = true;
        }
    }

    private static TextId ResultText(BatchResult result) => result switch
    {
        BatchResult.Completed => TextId.Completed,
        BatchResult.Stopped => TextId.Stopped,
        BatchResult.InventoryChanged => TextId.InventoryChanged,
        BatchResult.ContextChanged => TextId.ContextChanged,
        BatchResult.UnsafeState => TextId.UnsafeState,
        BatchResult.TimedOut => TextId.TimedOut,
        _ => TextId.RequestFailed,
    };

    public void Dispose()
    {
        disposed = true;
        pendingDiscard = null;
        Services.Framework.Update -= Update;
        Services.PluginInterface.UiBuilder.Draw -= Draw;
        Services.PluginInterface.UiBuilder.OpenMainUi -= OpenWindow;
        Services.PluginInterface.UiBuilder.OpenConfigUi -= OpenWindow;
        Services.Commands.RemoveHandler("/hoverdiscard");
        windows.RemoveAllWindows();
        window.Dispose();
    }
}
