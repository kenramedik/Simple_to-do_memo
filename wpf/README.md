# Simple To-Do Memo: native app (3.x)

C# and WPF on .NET 8. The tray icon uses Windows Forms' `NotifyIcon`. Features, install and migration notes are in the [main README](../README.md).

## Layout

| Path | What |
|---|---|
| `src/Core/` | Data model, storage, the carry-forward rules, strings (Korean/English), holidays, URL helpers |
| `src/Views/` | Main window, links window, dialogs, tray, shared UI helpers (`Ui.cs`) |
| `src/Themes/Theme.xaml` | Colors, button and input styles, menu, tooltip and scrollbar templates |
| `src/Fonts/` | Pretendard (SIL OFL 1.1, see `LICENSE.txt`) |

The data files use the same JSON shape as the Electron version, so importing is a straight copy.

## Build

Requires the .NET 8 SDK.

```bash
cd src
dotnet run                       # run in development
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:PublishReadyToRun=true -p:DebugType=none -o ../publish/full
dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -p:DebugType=none -o ../publish/lite
```

Don't add `PublishReadyToRun` to the framework-dependent (`lite`) build. That combination crashed at startup in testing.

## Scripted UI check

Debug builds include a scenario runner (`src/Views/TestDriver.cs`). Set `SIMPLETODOMEMO_APPDATA` to a scratch folder so your real data is untouched, and `SIMPLETODOMEMO_TEST` to an output folder. The app then walks through its main screens and writes screenshots plus `log.txt` there. The screenshots are rendered in-process, so the check also works while the screen is locked. It is not compiled into release builds.
