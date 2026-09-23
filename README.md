# Simple To-Do Memo (native)

A Windows desktop app for keeping a to-do list one day at a time, built as a native WPF application.

This is the native rewrite of [Simple To-Do Memo](https://github.com/kenramedik/Simple_to-do_memo), which runs on Electron. It has the same features, but the window, menus and tray icon are real Windows controls instead of a web page, so it starts faster and uses much less memory.

## Features

- **Day-by-day lists**: every date holds its own items. Move between days with the arrows, the Today button, the calendar, or the left/right arrow keys
- **Three-state checking**: click once to mark an item done, twice to mark it dropped, three times to clear it
- **Carries forward until you deal with it**: an unchecked item reappears on following weekdays until you mark it done or dropped. Weekends are skipped
- **Calendar**: shows how many items each day holds, and marks South Korean public holidays (2025–2030)
- **Pinning**: keep important items at the top, three per page, with a button that jumps to the item's date
- **Search**: `Ctrl+F` searches every date at once
- **Number links**: a number of five or more digits in parentheses, such as `(1234567)`, links to an address you set under `Settings → Link Settings…`
- **Link collection**: a separate window (`Ctrl+L`) for bookmarks. Name them, sort them into groups, drag them within or between groups, and search names and addresses
- **Colors**, **drag to reorder**, **delete lock**, **always on top** (`Ctrl+T`) and **minimize to tray**
- **English and Korean**

## Moving over from the Electron version

1. Update the Electron version to **2.12.0 or later** and run it once. It writes a copy of your tasks and links to `%APPDATA%\SimpleToDoMemo\export.json`.
2. Start this app. On its first launch it picks up that copy, along with your language, link and window settings.

To import again later, use `File → Import from Previous Version (Electron)…`. The two versions keep their data in separate folders, so you can run both while you try this one out.

## Install

Builds are published in the [Simple_to-do_memo releases](https://github.com/kenramedik/Simple_to-do_memo/releases) (version 3.0.0 and later).

- `SimpleToDoMemo-x.y.z-win-x64.zip`: unpack anywhere and run `SimpleToDoMemo.exe`. Everything it needs is inside. The first launch takes a few seconds longer while Windows unpacks a few runtime files
- `SimpleToDoMemo-x.y.z-win-x64-lite.exe`: a 6 MB build for machines that already have the [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0). If the runtime is missing, Windows offers to download it

The executable is unsigned, so SmartScreen may warn on first launch (`More info` → `Run anyway`).

Data lives in `%APPDATA%\SimpleToDoMemoWpf`.

## Development

Requires the .NET 8 SDK.

```bash
cd src
dotnet run                       # run in development
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:PublishReadyToRun=true -o ../publish/full
dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o ../publish/lite
```

Debug builds include a scripted UI check: set `SIMPLETODOMEMO_APPDATA` to a scratch folder (so your real data is untouched) and `SIMPLETODOMEMO_TEST` to an output folder, and the app runs through its main screens, writing screenshots and a log there.

## Built with

C# and WPF on .NET 8. The tray icon uses Windows Forms' `NotifyIcon`.

## License

MIT. Bundles [Pretendard](https://github.com/orioncactus/pretendard), licensed under the SIL Open Font License 1.1 (`src/Fonts/LICENSE.txt`).
