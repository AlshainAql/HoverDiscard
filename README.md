# HoverDiscard

A Dalamud plugin for selecting inventory items by hovering over them and discarding the selection with a single button.

## Usage

1. Open the plugin window with `/hoverdiscard` or through Dalamud's plugin settings.
2. Keep the **Discard list** tab visible, then hold **Shift** and hover over items in your inventory to add them to the discard list.
3. Review the selected items together with items matching your character's remembered rules.
4. Click **Discard checked items** to discard the checked items, one inventory slot at a time.

The selection covers all stacks of the same item and quality in your main inventory. For example, stacks of 99 and 20 are treated as a total of 119. **NQ, HQ, and collectables are tracked separately.**

Items are only discarded when you press the button. **Discarding is irreversible.**

The operation takes a fixed snapshot of your main inventory and waits for each discard to be confirmed. If the inventory changes unexpectedly, the character changes area, the game loses focus, or a request times out, it stops without retrying. **Stop** and closing the window prevent further requests; a request already sent to the game may still complete.

## Remembered items

Turn **Remember newly discarded items** on to remember item types after they have been successfully discarded. These rules are stored separately for each character and included the next time you use **Discard checked items**.

With this option off, newly selected item types apply to the current operation only. Previously remembered rules remain active. You can review and remove individual rules in the remembered-items list.

## Language and controls

- The interface supports **English** and **Japanese**, selectable in the **Settings** tab. The initial language follows the game client; item names always use the game client language.
- The default selection key is **Shift**, with **Ctrl** and **Alt** available as alternatives.
- The target is the four main inventory pages, including equipment stored there.
- Individual entries can be excluded from the current operation, and an active operation can be stopped.

## Bug reports

If you encounter a bug or other problem, please [open a GitHub issue](https://github.com/AlshainAql/HoverDiscard/issues/new/choose).

See [CONTRIBUTING.md](CONTRIBUTING.md) for details to include in your report.
