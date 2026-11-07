# HoverDiscard

A personal-use Dalamud plugin project for selecting inventory items by hovering over them and discarding the selection with a single button.

> **This plugin is intended for the author's personal use.**
> Feature requests, bug reports, questions, support requests, and external pull requests are not accepted.
> Development and updates follow the author's own needs.

## Planned workflow

1. Open the plugin window with `/hoverdiscard` or through Dalamud's plugin settings.
2. Hold **Shift** and hover over items in your inventory to add them to the discard list.
3. Review the selected items together with items matching your character's remembered rules.
4. Click **Discard all** to discard the checked items, one inventory slot at a time.

The selection covers all stacks of the same item and quality in your main inventory. For example, stacks of 99 and 20 are treated as a total of 119. **NQ and HQ are tracked separately.**

Items are only discarded when you press the button.

## Remembered items

Turn **Remember newly discarded items** on to remember item types after they have been successfully discarded. These rules are stored separately for each character and included the next time you use **Discard all**.

With this option off, newly selected item types apply to the current operation only. Previously remembered rules remain active. You can review and remove individual rules in the remembered-items list.

## Language and controls

- The interface will support **English** and **Japanese**, selectable in the plugin settings.
- The default selection key is **Shift**, with **Ctrl** and **Alt** available as alternatives.
- The target is the four main inventory pages, including equipment stored there.
- Individual entries can be excluded from the current operation, and an active operation can be stopped.

## Contributions and support

Issues and pull requests are restricted to collaborators and reserved for the author's own development work. External collaborators are not invited, and Discussions are disabled.

See [CONTRIBUTING.md](CONTRIBUTING.md) for the full policy.
