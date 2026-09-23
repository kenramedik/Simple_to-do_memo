using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Windows;
using SimpleToDoMemo.Core;
using SimpleToDoMemo.Views;

namespace SimpleToDoMemo;

public partial class App : Application
{
    public static readonly string Version =
        Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0] ?? "";

    const string MutexName = "SimpleToDoMemoWpf.SingleInstance";
    const string ShowEventName = "SimpleToDoMemoWpf.Show";

    Mutex? mutex;
    EventWaitHandle? showEvent;

    public Settings Settings { get; private set; } = new();
    public Memos Memos { get; private set; } = null!;
    public List<LinkGroup> Links { get; private set; } = null!;
    public MainWindow MainWin { get; private set; } = null!;
    LinksWindow? linksWin;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // 두 번째 실행이면 이미 떠 있는 창을 앞으로 불러오고 끝낸다
        mutex = new Mutex(true, MutexName, out bool first);
        if (!first)
        {
            try { EventWaitHandle.OpenExisting(ShowEventName).Set(); } catch { }
            Shutdown();
            return;
        }
        showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowEventName);
        var listener = new Thread(() =>
        {
            while (showEvent.WaitOne()) Dispatcher.BeginInvoke(() => MainWin?.ShowFromTray());
        }) { IsBackground = true };
        listener.Start();

        bool firstRun = !File.Exists(Path.Combine(Storage.Dir, "settings.json"));
        Settings = Storage.LoadSettings();
        Memos = new Memos(Storage.LoadMemos());
        Links = Storage.LoadLinks();

        // 처음 실행이면 Electron 판의 설정과 자료를 가져온다
        if (firstRun) ImportElectronSettings();
        if (firstRun && !Storage.HasData) ImportFromElectron(null, ask: false);

        if (Settings.Lang is not ("ko" or "en"))
        {
            var dlg = new LanguageWindow();
            Settings.Lang = dlg.ShowDialog() == true ? dlg.Choice : "ko";
        }
        L.Set(Settings.Lang);
        SaveSettings();

        MainWin = new MainWindow(this);
        // 링크 창만 남으면 메인 창을 되살릴 길이 없다 - 같이 닫는다
        MainWin.Closed += (_, _) => { mainClosed = true; Quit(); };
        MainWin.Show();
#if DEBUG
        TestDriver.StartIfRequested(this);
#endif
    }

    public void SaveSettings() => Storage.SaveSettings(Settings);
    public void SaveLinks() => Storage.SaveLinks(Links);

    // 창을 닫아 위치를 저장하게 한 뒤 끝낸다. 메인 창이 닫히면 Closed 에서 Shutdown 이 불린다.
    public void Quit()
    {
        linksWin?.Close();
        if (mainClosed) Shutdown();
        else MainWin.Close();
    }
    bool mainClosed;

    protected override void OnExit(ExitEventArgs e)
    {
        showEvent?.Dispose();
        mutex?.Dispose();
        base.OnExit(e);
    }

    /* ─── 창 사이에 걸친 설정 ─── */
    public void SetLang(string lang)
    {
        Settings.Lang = lang;
        L.Set(lang);
        SaveSettings();
        MainWin.Render();
        linksWin?.Render();
    }

    public void SetAlwaysOnTop(bool on)
    {
        Settings.AlwaysOnTop = on;
        SaveSettings();
        MainWin.Topmost = on;
        if (linksWin != null) linksWin.Topmost = on;
        MainWin.ApplyMenuState();
        MainWin.Render();
    }

    public void SetDeleteLock(bool on)
    {
        Settings.DeleteLock = on;
        SaveSettings();
        MainWin.Render();
        linksWin?.Render();
    }

    // 링크 모음 창 - 이미 떠 있으면 앞으로 가져온다
    public void OpenLinks()
    {
        if (linksWin != null)
        {
            if (linksWin.WindowState == WindowState.Minimized) linksWin.WindowState = WindowState.Normal;
            linksWin.Activate();
            return;
        }
        linksWin = new LinksWindow(this) { Topmost = Settings.AlwaysOnTop };
        linksWin.Closed += (_, _) => linksWin = null;
        linksWin.Show();
    }

    public void ShowAbout(Window owner)
    {
        var T = L.Cur;
        MessageBox.Show(owner,
            $"{L.Plain(T.AppTitle)}   v{Version}\n\n{T.CreatedBy}: goni\n.NET {Environment.Version}\n\n{T.AboutBody}",
            L.Plain(T.About), MessageBoxButton.OK, MessageBoxImage.Information);
    }

    /* ─── Electron 판에서 가져오기 ───
       Electron 판(2.12.0 이상)은 메모와 링크를 %APPDATA%\SimpleToDoMemo\export.json 에 함께 적어 둔다.
       브라우저 저장소(localStorage)는 다른 프로그램이 읽기 어려워 이 파일을 거친다. */
    static readonly string ElectronDir = Path.Combine(Storage.AppData, "SimpleToDoMemo");

    sealed class ElectronExport
    {
        [JsonPropertyName("memos")] public Dictionary<string, List<TodoItem>>? Memos { get; set; }
        [JsonPropertyName("links")] public LinksFile? Links { get; set; }
        [JsonPropertyName("newestFirst")] public bool? NewestFirst { get; set; }
    }

    public void ImportFromElectron(Window? owner, bool ask)
    {
        var T = L.Cur;
        var export = Storage.Read<ElectronExport>(Path.Combine(ElectronDir, "export.json"));
        if (export == null || (export.Memos == null && export.Links == null))
        {
            if (ask) MessageBox.Show(owner!, T.ImportNone, L.Plain(T.ImportElectron).TrimEnd('…'), MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        if (ask && MessageBox.Show(owner!, T.ImportAsk, L.Plain(T.ImportElectron).TrimEnd('…'),
                MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK) return;

        if (export.Memos != null) { Memos = new Memos(Storage.Normalize(export.Memos)); Memos.Save(); }
        if (export.Links != null) { Links = Storage.NormalizeLinks(export.Links); SaveLinks(); }
        if (export.NewestFirst is bool nf) Settings.NewestFirst = nf;
        Settings.MigratedFromElectron = true;
        SaveSettings();

        if (ask)
        {
            MainWin.Render();
            linksWin?.Reload();
            MessageBox.Show(owner!, T.ImportDone, L.Plain(T.ImportElectron).TrimEnd('…'), MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    // Electron 판의 settings.json 은 그대로 읽을 수 있다 - 언어, 항상 위, 링크 설정 등을 옮긴다
    void ImportElectronSettings()
    {
        try
        {
            var path = Path.Combine(ElectronDir, "settings.json");
            if (!File.Exists(path)) return;
            var old = JsonSerializer.Deserialize<Settings>(File.ReadAllText(path), Storage.Json);
            if (old == null) return;
            Settings.Lang = old.Lang;
            Settings.AlwaysOnTop = old.AlwaysOnTop;
            Settings.MinimizeToTray = old.MinimizeToTray;
            Settings.DeleteLock = old.DeleteLock;
            Settings.Link = old.Link ?? new NumberLink();
            Settings.Bounds = old.Bounds;
            Settings.LinksBounds = old.LinksBounds;
        }
        catch { }
    }
}
