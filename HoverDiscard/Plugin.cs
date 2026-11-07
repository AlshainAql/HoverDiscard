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
    internal TimeSpan Now => clock.Elapsed;
    internal TimeSpan LastSelectionDraw { get; set; } = TimeSpan.MinValue;
    internal bool AllowHover { get; set; }
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
    private void OnCommand(string command, string arguments) => OpenWindow();
    private void Draw() => windows.Draw();

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

    internal void Start()
    {
        if (Running || Inventory == null || Character == null || !Safe)
            return;
        var settings = Character;
        var included = Selection.Included(settings.RememberedItems);
        var newlySelected = Selection.NewItems.Except(settings.RememberedItems).ToHashSet();
        // Freeze the reviewed snapshot at the button click. The framework revalidates it
        // against fresh inventory data before it is allowed to send any request.
        Batch = new DiscardBatch(Inventory, included, newlySelected, settings.RememberNewItems,
            key =>
            {
                if (settings.RememberedItems.Add(key))
                    Save();
                if (SaveFailed)
                    Batch?.Stop();
            },
            key => Selection.NewItems.Remove(key));
        Notice = Batch.IsRunning ? null : TextId.NoItems;
        Stopping = false;
    }

    internal void Stop()
    {
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
                Batch?.Tick(Now, null, false, GameInventory.Discard);
                contentId = currentId;
                Selection.Reset();
                Stopping = false;
                Notice = null;
                Character = currentId == 0 ? null : Config.ForCharacter(currentId);
            }
            if (!window.IsOpen && !Running)
            {
                Inventory = null;
                Safe = false;
                return;
            }
            Inventory = Game.Read();
            Safe = Game.IsSafe && !ConfigurationInvalid && !SaveFailed;
            RuntimeError = false;
            loggedReadError = false;

            if (Running)
            {
                Batch!.Tick(Now, Inventory, Safe, GameInventory.Discard);
                if (!Running)
                {
                    Stopping = false;
                    Notice = ResultText(Batch.Result);
                    Services.Log.Information("Discard batch ended: {Result}; {Completed}/{Total} stacks.",
                        Batch.Result, Batch.CompletedStacks, Batch.TotalStacks);
                }
            }

            if (!Running && !ConfigurationInvalid && Inventory != null && Character != null && Safe
                && window.IsOpen && AllowHover && Now - LastSelectionDraw < TimeSpan.FromMilliseconds(250)
                && GameInventory.SelectionKeyDown(Config.SelectionKey)
                && GameInventory.HoveredInventoryItem(Inventory) is { } key)
                Selection.Add(key, Character.RememberedItems);
        }
        catch (Exception exception)
        {
            Inventory = null;
            Safe = false;
            RuntimeError = true;
            Batch?.Tick(Now, null, false, GameInventory.Discard);
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
        Services.Framework.Update -= Update;
        Services.PluginInterface.UiBuilder.Draw -= Draw;
        Services.PluginInterface.UiBuilder.OpenMainUi -= OpenWindow;
        Services.PluginInterface.UiBuilder.OpenConfigUi -= OpenWindow;
        Services.Commands.RemoveHandler("/hoverdiscard");
        windows.RemoveAllWindows();
        window.Dispose();
    }
}
