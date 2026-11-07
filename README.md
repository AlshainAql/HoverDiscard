# HoverDiscard

A Dalamud plugin for selecting inventory items by hovering over them and discarding the selection with a single button.

[![ko-fi](https://ko-fi.com/img/githubbutton_sm.svg)](https://ko-fi.com/B1S42A7KY6)

## Installation

1. Launch the game through XIVLauncher with Dalamud enabled.
2. Open Dalamud settings with `/xlsettings` and select **Experimental**.
3. Under **Custom Plugin Repositories**, paste the following URL and click the **+** button:

   ```text
   https://raw.githubusercontent.com/AlshainAql/HoverDiscard/main/pluginmaster.json
   ```

4. Click **Save changes and close**.
5. Open the plugin installer with `/xlplugins`, search for **HoverDiscard** under **All Plugins**, and click **Install**.
6. Open HoverDiscard with `/hoverdiscard`.

Updates are available through the same plugin installer. Release downloads are also available on [GitHub Releases](https://github.com/AlshainAql/HoverDiscard/releases).

## Usage

1. Open the plugin window with `/hoverdiscard` or through Dalamud's plugin settings.
2. Keep the **Discard list** tab visible, then hold **Shift** and hover over items in your inventory to add them to the discard list.
3. Review the selected items together with items matching your character's remembered rules. Use **All** or a category tab and the item-name/ID search to narrow the list.
4. Click **Discard checked items** to discard the checked items currently visible in the category and search results, one inventory slot at a time. Checked items hidden by the filters are not discarded.

The selection covers all stacks of the same item and quality in your main inventory. For example, stacks of 99 and 20 are treated as a total of 119. **NQ, HQ, and collectables are tracked separately.**

Items are only discarded when you press the button. **Discarding is irreversible.**

The operation takes a fixed snapshot of your main inventory and waits for each discard to be confirmed. If the inventory changes unexpectedly, the character changes area, the game loses focus, or a request times out, it stops without retrying. **Stop** and closing the window prevent further requests; a request already sent to the game may still complete.

## Remembered items

Turn **Remember newly discarded items** on to remember item types after they have been successfully discarded. These rules are stored separately for each character and included the next time you use **Discard checked items**.

With this option off, newly selected item types apply to the current operation only. Previously remembered rules remain active unless the item is whitelisted. The **Remembered items** tab lists all rules, including items you no longer hold, with category tabs and search. You can forget individual rules or protect an item directly from this list.

## Whitelist

The **Whitelist** protects items from both hover selection and bulk discarding. It is saved separately for each character and covers **all qualities of each item**, including NQ, HQ, and collectables.

- Open the **Whitelist** tab, hold your selection key, and hover over an item in your main inventory to protect it.
- Alternatively, click **Protect** on an item in the discard list or remembered-items list.
- Protected items are removed from any new selection and excluded from the discard list. Existing remembered rules stay visible as **Protected** and remain inactive.
- Click **Unprotect** in the whitelist to remove protection. Any remembered rules for that item become active again; previously cleared new selections are not restored.

The whitelist also has category tabs and search, and keeps items listed even when you no longer hold them. Protection cannot be changed while a discard operation is running.

## Language and controls

- The interface supports **English** and **Japanese**, selectable in the **Settings** tab. The initial language follows the game client. Category names follow the interface language; item names use the game client language.
- The default selection key is **Shift**, with **Ctrl** and **Alt** available as alternatives.
- The target is the four main inventory pages, including equipment stored there.
- Individual entries can be excluded from the current operation, and an active operation can be stopped.

## Bug reports

If you encounter a bug or other problem, please [open a GitHub issue](https://github.com/AlshainAql/HoverDiscard/issues/new/choose).

See [CONTRIBUTING.md](CONTRIBUTING.md) for details to include in your report.

## License

Licensed under the [BSD-3-Clause license](LICENSE).
