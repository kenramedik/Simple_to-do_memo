# Simple To-Do Memo

A Windows desktop app for keeping a to-do list one day at a time.

Each day gets its own list. Move between days with the arrows, the calendar, or the left/right arrow keys, and the app remembers what belongs to each date.

## Features

- **Day-by-day lists** — every date holds its own items; jump around with the arrows, the Today button, or the calendar
- **Three-state checking** — click once to mark an item done, twice to mark it dropped, three times to clear it
- **Carries forward until you deal with it** — an unchecked item reappears three weekdays later, and keeps coming back until you mark it done or dropped. Weekends are skipped, so a Wednesday item returns the following Monday
- **Calendar** — shows how many items each day holds, and marks South Korean public holidays (2025–2030) with a red tint and a tooltip naming the holiday
- **Pinning** — keep important items at the top regardless of the date. Paged three at a time, scrollable with the mouse wheel, with a button that jumps to the item's original date
- **Search** — `Ctrl+F` searches every date at once. Matches are highlighted, and clicking a result jumps to that day
- **Number links** — a number of five or more digits in parentheses, such as `(1234567)`, becomes a link. Set the address under `Settings → Link Settings…`: a URL and, optionally, the parameter name the number is passed as (`https://example.com/view` + `id` → `https://example.com/view?id=1234567`). Leave the parameter empty to append the number to the URL as-is. Clicking a link opens your default browser without checking the item
- **Colors** — tag any item with one of eight colors
- **Reordering** — drag items to rearrange them
- **Delete lock** — guards against accidental deletion; while it is on, the delete button is not rendered at all
- **Always on top** and **minimize to tray** — both toggled from the Settings menu
- **English and Korean** — the app asks which you want on first launch, and the `Language` menu switches at any time

Holiday data covers South Korean public holidays only.

## Install

Grab a build from [Releases](../../releases).

- `SimpleToDoMemo-Setup-x.y.z.exe` — installer. Runs without administrator rights and creates desktop and Start menu shortcuts
- `SimpleToDoMemo-x.y.z-win.zip` — portable. Unpack anywhere and run `SimpleToDoMemo.exe`

The executable is unsigned, so SmartScreen may warn on first launch (`More info` → `Run anyway`).

Upgrading from a version named DateMemo carries your existing notes over automatically on first launch.

## Development

```bash
npm install
npm start        # run in development
npm run icon     # regenerate the icons in assets/
npm run dist     # build the installer and the zip
```

Data lives in `%APPDATA%\SimpleToDoMemo`.

## Built with

Electron 33 and plain HTML, CSS, and JavaScript — no framework, no bundler. `index.html` holds the entire interface.

## License

MIT. Bundles [Pretendard](https://github.com/orioncactus/pretendard), licensed under the SIL Open Font License 1.1.
