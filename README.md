# Simple To-Do Memo

A Windows desktop app for keeping a to-do list one day at a time.

Each day gets its own list. Move between days with the arrows, the calendar, or the left/right arrow keys, and the app remembers what belongs to each date.

> [!IMPORTANT]
> **Upgrading from 1.x or 2.x? Your notes do not move over on their own.** 3.x starts with an empty list. Bring them across with **`File → Import from Previous Version (Electron)…`**.
>
> **Don't uninstall the old version or delete `%APPDATA%\SimpleToDoMemo` before importing.** That folder is where the import reads from. See [Moving over from 1.x / 2.x](#moving-over-from-1x--2x-electron) below.

Since 3.0.0 it is a native Windows app written in C# and WPF (`wpf/`). The window, menu bar, right-click menus and tray icon are real Windows controls. Versions 1.x–2.x ran on Electron, and that code is still in the repository root.

## Features

- **Day-by-day lists**: every date holds its own items. Jump around with the arrows, the Today button, or the calendar
- **Three-state checking**: click once to mark an item done, twice to mark it dropped, three times to clear it
- **Carries forward until you deal with it**: an unchecked item reappears on following weekdays, and keeps coming back until you mark it done or dropped. Weekends are skipped
- **Calendar**: shows how many items each day holds, and marks South Korean public holidays (2025–2030) with a red tint and a tooltip naming the holiday
- **Pinning**: keep important items at the top regardless of the date. Paged three at a time, scrollable with the mouse wheel, with a button that jumps to the item's original date
- **Search**: `Ctrl+F` searches every date at once. Matches are highlighted, and clicking a result jumps to that day
- **Number links**: a number of five or more digits in parentheses, such as `(1234567)`, becomes a link. Set the address under `Settings → Link Settings…`: a URL and, optionally, the parameter name the number is passed as (`https://example.com/view` + `id` → `https://example.com/view?id=1234567`). Leave the parameter empty to append the number to the URL as-is. Clicking a link opens your default browser without checking the item
- **Link collection**: a separate window (`Ctrl+L`, the link button, or `File → Links`) for bookmarks you use often. Give each one a short name and an address. The name is optional and falls back to the site name. Sort links into groups, drag them within or between groups, and search names and addresses at once. Deleting a group moves its links to Ungrouped rather than deleting them
- **Colors**: tag any item with one of eight colors
- **Reordering**: drag items to rearrange them
- **Delete lock**: guards against accidental deletion. While it is on, delete options are not shown at all
- **Always on top** (`Ctrl+T`) and **minimize to tray**, both toggled from the Settings menu
- **English and Korean**: the app asks which you want on first launch, and the `Language` menu switches at any time

Holiday data covers South Korean public holidays only.

## Install

Grab a build from [Releases](../../releases).

- `SimpleToDoMemo-x.y.z-win-x64.zip`: unpack anywhere and run `SimpleToDoMemo.exe`. No other install needed. The first launch takes a few seconds while Windows unpacks some runtime files
- `SimpleToDoMemo-x.y.z-win-x64-lite.exe`: a 6 MB build for machines that have the [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0). If the runtime is missing, Windows offers to download it

The executable is unsigned, so SmartScreen may warn on first launch (`More info` → `Run anyway`).

Data lives in `%APPDATA%\SimpleToDoMemoWpf`.

### Moving over from 1.x / 2.x (Electron)

1. Start 3.x.
2. Choose `File → Import from Previous Version (Electron)…` and confirm.

This works with any earlier version, including the ones named DateMemo, and you don't need to run the old version first. 3.1.0 and later read the old version's storage (`%APPDATA%\SimpleToDoMemo`) directly. The import brings over your tasks, links, sort order and number-link address. It replaces whatever is in 3.x at that moment, and asks before doing so.

The two versions keep their data in separate folders, so the old version keeps working until you remove it. Only remove it after checking that the import came across.

## Repository layout

| Path | What |
|---|---|
| `wpf/` | The native app (3.x). C# and WPF on .NET 8. See [wpf/README.md](wpf/README.md) for building and testing |
| root (`main.js`, `index.html`, `links.html`, …) | The Electron app (2.x). Kept so 2.x can still be patched |

### Electron (2.x) development

```bash
npm install
npm start        # run in development
npm run icon     # regenerate the icons in assets/
npm run dist     # build the installer and the zip
```

Plain HTML, CSS and JavaScript on Electron 33, with no framework and no bundler. Its data lives in `%APPDATA%\SimpleToDoMemo`.

## License

MIT. Bundles [Pretendard](https://github.com/orioncactus/pretendard), licensed under the SIL Open Font License 1.1.
