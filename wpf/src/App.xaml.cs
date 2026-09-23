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

        // Electron 판 자료는 저절로 가져오지 않는다 - 파일 메뉴의 '이전 버전에서 가져오기'로만 옮긴다
        Settings = Storage.LoadSettings();
        Memos = new Memos(Storage.LoadMemos());
        Links = Storage.LoadLinks();

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
       Electron 판의 브라우저 저장소(localStorage)를 직접 읽는다 (ElectronStore). 어느 버전이든, 앱을 따로
       실행하지 않아도 된다. 읽지 못하면 2.12.0 이 남겨 둔 export.json 사본으로 대신한다. */
    static readonly string ElectronDir = Path.Combine(Storage.AppData, "SimpleToDoMemo");

    sealed class ElectronExport
    {
        [JsonPropertyName("memos")] public Dictionary<string, List<TodoItem>>? Memos { get; set; }
        [JsonPropertyName("links")] public LinksFile? Links { get; set; }
        [JsonPropertyName("newestFirst")] public bool? NewestFirst { get; set; }
    }

    static ElectronExport? ReadElectron()
    {
        var raw = ElectronStore.Read(Storage.AppData);
        if (raw != null)
        {
            var ex = new ElectronExport();
            try { if (raw.Memos != null) ex.Memos = JsonSerializer.Deserialize<Dictionary<string, List<TodoItem>>>(raw.Memos, Storage.Json); } catch { }
            try { if (raw.Links != null) ex.Links = JsonSerializer.Deserialize<LinksFile>(raw.Links, Storage.Json); } catch { }
            if (raw.Sort != null) ex.NewestFirst = raw.Sort == "desc";
            if (ex.Memos != null || ex.Links != null) return ex;
        }
        return Storage.Read<ElectronExport>(Path.Combine(ElectronDir, "export.json"));
    }

    public void ImportFromElectron(Window? owner, bool ask)
    {
        var T = L.Cur;
        var export = ReadElectron();
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
        // 숫자 링크 주소는 자료에 딸린 설정이라 함께 옮긴다. 언어·항상 위 등은 이 판에서 고른 대로 둔다.
        if (ReadElectronLink() is { Url.Length: > 0 } link) Settings.Link = link;
        Settings.MigratedFromElectron = true;
        SaveSettings();

        MainWin?.Render();
        linksWin?.Reload();
        if (ask) MessageBox.Show(owner!, T.ImportDone, L.Plain(T.ImportElectron).TrimEnd('…'), MessageBoxButton.OK, MessageBoxImage.Information);
    }

    // Electron 판의 settings.json 은 그대로 읽을 수 있다. 2.2.0 전에는 이름이 DateMemo 였다.
    static NumberLink? ReadElectronLink()
    {
        foreach (var dir in new[] { ElectronDir, Path.Combine(Storage.AppData, "DateMemo") })
        {
            var old = Storage.Read<Settings>(Path.Combine(dir, "settings.json"));
            if (old != null) return old.Link;
        }
        return null;
    }
}
