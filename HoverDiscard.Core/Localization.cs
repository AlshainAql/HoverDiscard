using System.Globalization;

namespace HoverDiscard.Core;

public enum TextId
{
    DiscardList, RememberedItems, Settings, Instructions, RememberNew, RememberHint,
    ClearSelection, DiscardAll, Stop, Irreversible, Login, InventoryUnavailable,
    Unsafe, EmptyList, EmptyRules, Use, Item, Quality, Quantity, Stacks, Source, Saved, New,
    NormalQuality, HighQuality, Collectable, CannotDiscard, MissingItem, Remove, Progress,
    Completed, Stopped, InventoryChanged, ContextChanged, UnsafeState, TimedOut, RequestFailed,
    Language, SelectionKey, SelectionKeyHint, SaveFailed, RetrySave, ConfigInvalid, RuntimeError,
    SelectedTotal, RememberedCount, RuleHint, ActiveHint, NoItems, ExcludedHint, ClearHint,
    PendingStop, CommandHelp,
    CommandStarted, AlreadyRunning, NotRunning, CommandCancelled, NoRememberedTargets, ShowInventoryButton, InventoryButtonHint, InventoryButtonTooltip, DiscardRemembered, OpenPlugin, RememberedTargetTotal, CommandScopeHint,
    Whitelist, WhitelistInstructions, WhitelistHint, WhitelistCount, EmptyWhitelist, Protect, ProtectHint, Protected, ProtectedHint, Unprotect, UnprotectHint, AllCategories, OtherCategory, SearchHint, NoMatches, Status, FilteredDiscardHint,
}

public static class Localization
{
    private static readonly Dictionary<TextId, (string English, string Japanese)> Text = new()
    {
        [TextId.Whitelist] = ("Whitelist", "ホワイトリスト"),
        [TextId.WhitelistInstructions] = ("Hold {0} and hover over a main-inventory item to protect it.", "所持品のアイテムにマウスを重ねながら {0} を押すと、ホワイトリストに登録します。"),
        [TextId.WhitelistHint] = ("Protection applies to this character and all qualities (NQ, HQ, and collectables). Protected items cannot enter the discard list, even if remembered.", "保護はこのキャラクターのNQ・HQ・収集品すべてに適用されます。記憶済みでも、保護中のアイテムは破棄リストに追加されません。"),
        [TextId.WhitelistCount] = ("Protected item types: {0}", "保護中のアイテム：{0} 種類"),
        [TextId.EmptyWhitelist] = ("No protected items. Hold the selection key over inventory items, or use Protect in another list.", "保護中のアイテムはありません。選択キー＋MO、または他の一覧の「保護する」から登録できます。"),
        [TextId.Protect] = ("Protect", "保護する"),
        [TextId.ProtectHint] = ("Add to the whitelist for all qualities and remove any new selection. Remembered rules are kept but inactive while protected.", "全品質をホワイトリストに登録し、今回の選択から外します。記憶済みのルールは残りますが、保護中は適用されません。"),
        [TextId.Protected] = ("Protected", "保護中"),
        [TextId.ProtectedHint] = ("This remembered rule is inactive because the item is whitelisted.", "ホワイトリストに登録されているため、この記憶ルールは適用されません。"),
        [TextId.Unprotect] = ("Unprotect", "保護を解除"),
        [TextId.UnprotectHint] = ("Remove from the whitelist. Any remembered rules for this item become active again.", "ホワイトリストから外します。このアイテムの記憶ルールが残っている場合は、再び適用されます。"),
        [TextId.AllCategories] = ("All", "すべて"),
        [TextId.OtherCategory] = ("Other", "その他"),
        [TextId.SearchHint] = ("Search by item name or ID", "アイテム名・IDで検索"),
        [TextId.NoMatches] = ("No items match this category and search.", "このカテゴリー・検索条件に一致するアイテムはありません。"),
        [TextId.Status] = ("Status", "状態"),
        [TextId.FilteredDiscardHint] = ("Only checked items in the current category and search results will be discarded.", "現在のカテゴリー・検索結果に表示されている、チェック済みのアイテムだけを破棄します。"),
        [TextId.CommandStarted] = ("Discarding {0} stacks matching remembered rules. Use /hoverdiscard stop to stop.", "記憶済みルールに一致する {0} 枠の破棄を開始します。停止は /hoverdiscard stop です。"),
        [TextId.AlreadyRunning] = ("A discard operation is already running or waiting to start.", "破棄処理はすでに実行中、または開始待ちです。"),
        [TextId.NotRunning] = ("No discard operation is running.", "実行中の破棄処理はありません。"),
        [TextId.CommandCancelled] = ("Cancelled the discard request before it started.", "開始前の破棄要求を取り消しました。"),
        [TextId.NoRememberedTargets] = ("No eligible remembered items are currently held. Whitelisted and unchecked items are excluded.", "現在の所持品に破棄対象の記憶済みアイテムがありません。保護中・チェックを外したアイテムは対象外です。"),
        [TextId.ShowInventoryButton] = ("Show the button beside the inventory window", "所持品ウィンドウの横にボタンを表示"),
        [TextId.InventoryButtonHint] = ("The button follows the left edge of the main inventory window. Open its menu to discard remembered items, stop, or open HoverDiscard.", "所持品ウィンドウの左側にボタンを表示します。メニューから記憶済みアイテムの破棄・停止・HoverDiscardの表示を選べます。"),
        [TextId.InventoryButtonTooltip] = ("HoverDiscard: remembered-item actions", "HoverDiscard：記憶済みアイテムの操作"),
        [TextId.DiscardRemembered] = ("Discard remembered items", "記憶済みアイテムを破棄"),
        [TextId.OpenPlugin] = ("Open HoverDiscard", "HoverDiscardを開く"),
        [TextId.RememberedTargetTotal] = ("Remembered targets: {0} types, {1} items in {2} stacks", "記憶済みの対象：{0} 種類・合計 {1} 個（{2} 枠）"),
        [TextId.CommandScopeHint] = ("Uses remembered rules across all categories. Whitelisted and unchecked items are excluded; new selections are not included.", "全カテゴリーの記憶済みルールを使います。保護中・チェックを外したアイテムは除外し、今回の追加分は含めません。"),
        [TextId.CommandHelp] = ("/hoverdiscard: open the window. /hoverdiscard discard: discard held items matching remembered rules. /hoverdiscard stop: stop. /hoverdiscard help: show this help.", "/hoverdiscard：画面を開く。/hoverdiscard discard：所持品のうち記憶済みのアイテムを破棄。/hoverdiscard stop：停止。/hoverdiscard help：この説明を表示。"),
        [TextId.DiscardList] = ("Discard list", "破棄リスト"),
        [TextId.RememberedItems] = ("Remembered items", "記憶したアイテム"),
        [TextId.Settings] = ("Settings", "設定"),
        [TextId.Instructions] = ("Hold {0} and hover over an item in your main inventory to add every stack of that item and quality.", "所持品のアイテムにマウスを重ねながら {0} を押すと、同じアイテム・品質の全スタックを追加します。"),
        [TextId.RememberNew] = ("Remember newly discarded items", "今回破棄したアイテムを記憶する"),
        [TextId.RememberHint] = ("A new rule is saved after a stack is successfully discarded. Turning this off keeps existing rules active.", "破棄の成功を確認すると、このキャラクターのルールとして保存します。オフにしても、すでに記憶したルールは有効です。"),
        [TextId.ClearSelection] = ("Clear new selection", "今回の追加分をクリア"),
        [TextId.DiscardAll] = ("Discard checked items", "チェックしたアイテムを一括破棄"),
        [TextId.Stop] = ("Stop", "停止"),
        [TextId.Irreversible] = ("Discarding is irreversible. Review the checked items and quantities before starting.", "破棄したアイテムは元に戻せません。実行前にチェックと数量を確認してください。"),
        [TextId.Login] = ("Log in to select items and manage this character's rules.", "アイテムの選択やルールの管理にはログインが必要です。"),
        [TextId.InventoryUnavailable] = ("Waiting for the four main inventory pages to load.", "所持品の4ページが読み込まれるのを待っています。"),
        [TextId.Unsafe] = ("Return to normal gameplay and focus the game window before starting.", "操作可能な状態に戻り、ゲーム画面をアクティブにしてから実行してください。"),
        [TextId.EmptyList] = ("Hold the selection key over inventory items to add them here.", "選択キーを押しながら所持品にマウスを重ねると、ここに追加されます。"),
        [TextId.EmptyRules] = ("No remembered items for this character.", "このキャラクターで記憶したアイテムはありません。"),
        [TextId.Use] = ("Use", "対象"),
        [TextId.Item] = ("Item", "アイテム"),
        [TextId.Quality] = ("Quality", "品質"),
        [TextId.Quantity] = ("Quantity", "数量"),
        [TextId.Stacks] = ("Stacks", "枠数"),
        [TextId.Source] = ("Source", "登録"),
        [TextId.Saved] = ("Remembered", "記憶済み"),
        [TextId.New] = ("New", "今回追加"),
        [TextId.NormalQuality] = ("NQ", "NQ"),
        [TextId.HighQuality] = ("HQ", "HQ"),
        [TextId.Collectable] = ("Collectable", "収集品"),
        [TextId.CannotDiscard] = ("This item cannot be discarded.", "このアイテムは破棄できません。"),
        [TextId.MissingItem] = ("Item #{0}", "アイテム #{0}"),
        [TextId.Remove] = ("Forget", "記憶を削除"),
        [TextId.Progress] = ("Discarded {0} / {1} stacks.", "{1} 枠中 {0} 枠を破棄しました。"),
        [TextId.Completed] = ("Finished discarding the selected items.", "選択したアイテムの破棄が完了しました。"),
        [TextId.Stopped] = ("Stopped. Remaining items are still selected.", "停止しました。残りのアイテムはリストに残っています。"),
        [TextId.InventoryChanged] = ("Stopped because the inventory changed. Review the list before trying again.", "所持品が変化したため停止しました。リストを確認してから再実行してください。"),
        [TextId.ContextChanged] = ("Stopped because the character, area, or inventory became unavailable.", "キャラクターやエリアの変化、または所持品を取得できない状態になったため停止しました。"),
        [TextId.UnsafeState] = ("Stopped because the game lost focus or the character became busy.", "ゲーム画面が非アクティブになったか、操作できない状態になったため停止しました。"),
        [TextId.TimedOut] = ("Stopped: the last discard could not be confirmed within 5 seconds. No retry was sent.", "5秒以内に破棄を確認できなかったため停止しました。再送は行っていません。"),
        [TextId.RequestFailed] = ("Stopped because the discard request could not be sent.", "破棄の要求を送信できなかったため停止しました。"),
        [TextId.Language] = ("Language", "表示言語"),
        [TextId.SelectionKey] = ("Selection key", "選択キー"),
        [TextId.SelectionKeyHint] = ("The key adds items to the visible Discard list or Whitelist tab. Discard rules track qualities separately; whitelist protection covers all qualities.", "選択キーは、表示中の「破棄リスト」または「ホワイトリスト」に追加します。破棄ルールは品質別、ホワイトリストの保護は全品質共通です。"),
        [TextId.SaveFailed] = ("Settings could not be saved. Retry saving before closing the plugin.", "設定の保存に失敗しています。プラグインを閉じる前に保存を再試行してください。"),
        [TextId.RetrySave] = ("Retry saving", "保存を再試行"),
        [TextId.ConfigInvalid] = ("The saved configuration could not be read. Discarding and saving are disabled to preserve it. Check the plugin log.", "保存済みの設定を読み込めませんでした。設定を保護するため、破棄と保存を停止しています。プラグインログを確認してください。"),
        [TextId.RuntimeError] = ("The inventory could not be read safely. Check the plugin log.", "所持品を安全に読み取れませんでした。プラグインログを確認してください。"),
        [TextId.SelectedTotal] = ("Checked: {0} item types, {1} items in {2} stacks", "対象：{0} 種類・合計 {1} 個（{2} 枠）"),
        [TextId.RememberedCount] = ("Remembered item types: {0}", "記憶したアイテム：{0} 種類"),
        [TextId.RuleHint] = ("Rules apply to this character only. Items not currently held remain listed here.", "ルールはこのキャラクターにのみ適用されます。現在所持していないアイテムも表示します。"),
        [TextId.ActiveHint] = ("Keep the game focused. Inventory changes stop the operation.", "ゲーム画面をアクティブに保ってください。所持品が変化すると停止します。"),
        [TextId.NoItems] = ("No checked items are available to discard.", "破棄できる対象がありません。"),
        [TextId.ExcludedHint] = ("Uncheck to skip this run without forgetting the item.", "チェックを外すと、記憶を残したまま今回の対象から除外します。"),
        [TextId.ClearHint] = ("Clears newly selected items. Remembered rules and their checkboxes are kept.", "今回追加したアイテムをクリアします。記憶済みのルールとそのチェック状態は維持します。"),
        [TextId.PendingStop] = ("Stopping after the request already sent to the game is resolved.", "送信済みの破棄要求の結果を確認してから停止します。"),
    };

    public static string Get(UiLanguage language, TextId id, params object[] args)
    {
        var pair = Text[id];
        var format = language == UiLanguage.Japanese && !string.IsNullOrEmpty(pair.Japanese)
            ? pair.Japanese : pair.English;
        return args.Length == 0 ? format : string.Format(CultureInfo.InvariantCulture, format, args);
    }
}
