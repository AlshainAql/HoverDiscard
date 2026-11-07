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
}

public static class Localization
{
    private static readonly Dictionary<TextId, (string English, string Japanese)> Text = new()
    {
        [TextId.CommandHelp] = ("Open HoverDiscard.", "HoverDiscardを開きます。"),
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
        [TextId.SelectionKeyHint] = ("Selection works only while the Discard list tab is visible. NQ, HQ, and collectables are separate.", "「破棄リスト」タブを表示している間だけ選択できます。NQ・HQ・収集品は別々に扱います。"),
        [TextId.SaveFailed] = ("Items may have been discarded, but settings could not be saved. Retry saving before closing the plugin.", "破棄が成功していても、設定の保存に失敗しています。プラグインを閉じる前に保存を再試行してください。"),
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
