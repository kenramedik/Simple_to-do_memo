#if DEBUG
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace SimpleToDoMemo.Views;

/* 디버그 빌드 전용 - SIMPLETODOMEMO_TEST 에 폴더를 주면 정해진 시나리오를 돌리며 화면을 그 폴더에 찍는다.
   화면이 잠겨 있어도 돌 수 있게 실제 화면 대신 창 내용을 직접 그려(RenderTargetBitmap) 찍고,
   마우스 동작은 이벤트를 직접 일으켜 흉내 낸다. 릴리스 빌드에는 들어가지 않는다. */
static class TestDriver
{
    static string outDir = "";
    static readonly List<string> log = new();

    public static void StartIfRequested(App app)
    {
        var dir = Environment.GetEnvironmentVariable("SIMPLETODOMEMO_TEST");
        if (string.IsNullOrEmpty(dir)) return;
        outDir = dir;
        Directory.CreateDirectory(outDir);
        app.MainWin.Dispatcher.BeginInvoke(async () =>
        {
            try { await Run(app); }
            catch (Exception e) { log.Add("EXCEPTION " + e); }
            File.WriteAllLines(Path.Combine(outDir, "log.txt"), log);
            app.Quit();
        }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
    }

    static Task Wait(int ms = 200) => Task.Delay(ms);

    // 1.5배로 그려 글자까지 또렷하게 본다
    static void Shot(FrameworkElement el, string name)
    {
        el.UpdateLayout();
        const double k = 1.5;
        var rtb = new RenderTargetBitmap((int)Math.Ceiling(el.ActualWidth * k), (int)Math.Ceiling(el.ActualHeight * k), 96 * k, 96 * k, PixelFormats.Pbgra32);
        rtb.Render(el);
        var enc = new PngBitmapEncoder();
        enc.Frames.Add(BitmapFrame.Create(rtb));
        using var fs = File.Create(Path.Combine(outDir, name + ".png"));
        enc.Save(fs);
        log.Add("shot " + name);
    }
    static void Shot(Window w, string name) => Shot((FrameworkElement)w.Content, name);

    static void Raise(UIElement el, RoutedEvent ev, MouseButton? button = null)
    {
        RoutedEventArgs args = button is MouseButton b
            ? new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, b) { RoutedEvent = ev }
            : new MouseEventArgs(Mouse.PrimaryDevice, Environment.TickCount) { RoutedEvent = ev };
        el.RaiseEvent(args);
    }
    static void Hover(UIElement el) => Raise(el, Mouse.MouseEnterEvent);
    static void Unhover(UIElement el) => Raise(el, Mouse.MouseLeaveEvent);
    static void Press(UIElement el) => Raise(el, UIElement.MouseLeftButtonDownEvent, MouseButton.Left);
    static void ClickEl(UIElement el) { Press(el); Raise(el, UIElement.MouseLeftButtonUpEvent, MouseButton.Left); }
    static void RightClick(UIElement el) => Raise(el, UIElement.MouseRightButtonUpEvent, MouseButton.Right);
    static void ClickBtn(ButtonBase b) => b.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));

    static object? Call(object o, string name, params object[] args) =>
        o.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)!.Invoke(o, args);

    static ContextMenu? OpenMenu() => PresentationSource.CurrentSources.OfType<System.Windows.Interop.HwndSource>()
        .Select(s => s.RootVisual).OfType<FrameworkElement>()
        .SelectMany(v => Find<ContextMenu>(v).Prepend(v as ContextMenu)).FirstOrDefault(m => m is { IsOpen: true });

    static IEnumerable<T> Find<T>(DependencyObject root) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var c = VisualTreeHelper.GetChild(root, i);
            if (c is T t) yield return t;
            foreach (var x in Find<T>(c)) yield return x;
        }
    }

    static Border Card(Panel list, int i) => (Border)((Grid)list.Children[i]).Children[0];

    // 끌기: 행에서 누른 뒤 목표 지점(목록 좌표)으로 옮기고, 놓기 전에 한 장 찍는다
    static async Task Drag(Window w, ScrollViewer scroll, Panel list, Border card, Point to, string shot)
    {
        // 레이아웃이 바뀌면 WPF 가 가짜 MouseMove 를 보내는데, 실제 버튼은 안 눌려 있어 끌기가 취소된다.
        // 테스트에서만 실제 마우스 처리기를 떼어 낸다.
        var real = Delegate.CreateDelegate(typeof(MouseEventHandler), w, "OnDragMove");
        scroll.RemoveHandler(UIElement.PreviewMouseMoveEvent, real);
        Press(card);
        object? F(string name) => w.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(w);
        log.Add("  after press: drag=" + (F("drag") != null));
        var start = card.TranslatePoint(new Point(card.ActualWidth / 2, card.ActualHeight / 2), list);
        var mid = new Point(start.X, (start.Y + to.Y) / 2);
        double Y(Point p) => list.TranslatePoint(p, scroll).Y;
        ((dynamic)w).DragTo(mid, Y(mid));
        log.Add("  after move1: drag=" + (F("drag") != null) + " captured=" + scroll.IsMouseCaptured);
        await Wait(60);
        ((dynamic)w).DragTo(to, Y(to));
        await Wait(150);
        log.Add("  after move2: drag=" + (F("drag") != null) + " dropTo=" + (F(w is MainWindow ? "dropTo" : "dropAt") ?? "null"));
        Shot(w, shot);
        Call(w, "EndDrag", true);
        await Wait(200);
    }

    static string Order(App app) => string.Join(" | ", app.Memos.Data["2026-09-23"].Select(x => x.Text.Length > 12 ? x.Text[..12] : x.Text));
    static string LinkOrder(App app) => string.Join(" / ", app.Links.Select(g => (g.Id == "inbox" ? "INBOX" : g.Name) + ":" + string.Join(",", g.Links.Select(l => l.Name))));

    static async Task Run(App app)
    {
        var w = app.MainWin;
        // 가져오기만 확인하는 모드 - 처음 실행에서 Electron 판 자료를 읽어 왔는지 적고 끝낸다
        if (Environment.GetEnvironmentVariable("SIMPLETODOMEMO_TEST_MODE") == "import")
        {
            await Wait(400);
            log.Add("memos: " + string.Join(" ; ", app.Memos.Data.OrderBy(p => p.Key).Select(p => p.Key + "=" + string.Join(",", p.Value.Select(i => $"{i.Text}[{i.State}{(i.Color != null ? "," + i.Color : "")}{(i.PinnedAt != null ? ",pin" : "")}]")))));
            log.Add("links: " + LinkOrder(app) + " collapsed=" + string.Join(",", app.Links.Select(g => g.Collapsed)));
            log.Add($"settings: lang={app.Settings.Lang} newestFirst={app.Settings.NewestFirst} link={app.Settings.Link.Url}?{app.Settings.Link.Param} deleteLock={app.Settings.DeleteLock} migrated={app.Settings.MigratedFromElectron}");
            Shot(w, "i01-imported");
            return;
        }
        await Wait(500);
        Shot(w, "m01-main");

        Hover(Card(w.ListPanel, 1));
        await Wait(100);
        Shot(w, "m02-hover");
        Unhover(Card(w.ListPanel, 1));

        RightClick(Card(w.ListPanel, 1));
        await Wait(300);
        var menu = OpenMenu();
        log.Add("ctx menu open: " + (menu != null) + " items " + menu?.Items.Count);
        if (menu != null) { Shot(menu, "m03-ctx"); menu.IsOpen = false; }

        ClickBtn(w.MonthBtn);
        await Wait(200);
        Shot(w, "m04-calendar");
        ClickBtn(w.CalClose);

        Call(w, "OpenSearch");
        w.SearchInput.Text = "리뷰";
        await Wait(200);
        Shot(w, "m05-search");
        Call(w, "CloseSearch");
        await Wait(100);

        // 끌어서 순서 바꾸기 - 넘어온 항목 다음의 첫 자기 항목을 맨 아래로
        int n = w.ListPanel.Children.Count;
        log.Add("rows " + n);
        log.Add("order before: " + Order(app));
        var last = (Grid)w.ListPanel.Children[n - 1];
        var target = last.TranslatePoint(new Point(last.ActualWidth / 2, last.ActualHeight * .85), w.ListPanel);
        await Drag(w, w.ListScroll, w.ListPanel, Card(w.ListPanel, 1), target, "m06-dragging");
        log.Add("order after:  " + Order(app));
        Shot(w, "m07-after-drag");

        w.Input.Text = "새로 추가한 할 일";
        Call(w, "AddItem");
        await Wait(200);
        Shot(w, "m08-added");
        log.Add("order added:  " + Order(app));

        // 체크 순환: 미확인 -> 완료 -> 드랍 -> 미확인
        var cyc = Card(w.ListPanel, w.ListPanel.Children.Count - 1);
        var states = new List<int>();
        for (int i = 0; i < 3; i++)
        {
            ClickEl(Card(w.ListPanel, w.ListPanel.Children.Count - 1));
            await Wait(80);
            states.Add(app.Memos.Data["2026-09-23"][^1].State);
        }
        log.Add("cycle states: " + string.Join(",", states));

        // 메뉴바 드롭다운
        w.MSettings.IsSubmenuOpen = true;
        await Wait(300);
        var pop = Find<Popup>(w.MSettings).FirstOrDefault();
        if (pop?.Child is FrameworkElement pc) Shot(pc, "m09-menu");
        w.MSettings.IsSubmenuOpen = false;

        // 색 선택
        Call(w, "OpenColorPop", new Point(120, 300), app.Memos.Data["2026-09-23"][0]);
        await Wait(300);
        if (w.ColorPop.Child is FrameworkElement cp) Shot(cp, "m10-colors");
        w.ColorPop.IsOpen = false;

        // 링크 모음 창
        app.OpenLinks();
        await Wait(600);
        var lw = Application.Current.Windows.OfType<LinksWindow>().First();
        Shot(lw, "l01-links");

        var cards = Find<Border>(lw.ListPanel).Where(b => b.MinHeight == 50).ToList();
        var heads = Find<Border>(lw.ListPanel).Where(b => b.Height == 30).ToList();
        log.Add($"link cards {cards.Count}, heads {heads.Count}");
        log.Add("links before: " + LinkOrder(app));
        // 첫 링크를 마지막(빈) 그룹의 빈 칸으로
        var emptyBox = Find<Border>(lw.ListPanel).Last(b => b.CornerRadius.TopLeft == 11 && b.MinHeight != 50);
        var to = emptyBox.TranslatePoint(new Point(emptyBox.ActualWidth / 2, emptyBox.ActualHeight / 2), lw.ListPanel);
        await Drag(lw, lw.ListScroll, lw.ListPanel, cards[0], to, "l02-dragging");
        log.Add("links drag1:  " + LinkOrder(app));
        // 업무 그룹의 둘째 링크를 첫째 위로
        cards = Find<Border>(lw.ListPanel).Where(b => b.MinHeight == 50).ToList();
        var jira = cards.First(c => Find<TextBlock>(c).Any(t => t.Text == "" && t.Inlines.OfType<System.Windows.Documents.Run>().Any(r => r.Text == "Jira")));
        var wiki = cards.First(c => Find<TextBlock>(c).Any(t => t.Inlines.OfType<System.Windows.Documents.Run>().Any(r => r.Text == "사내 위키")));
        to = wiki.TranslatePoint(new Point(wiki.ActualWidth / 2, 8), lw.ListPanel);
        await Drag(lw, lw.ListScroll, lw.ListPanel, jira, to, "l03-dragging-line");
        log.Add("links drag2:  " + LinkOrder(app));
        Shot(lw, "l04-after-drag");

        cards = Find<Border>(lw.ListPanel).Where(b => b.MinHeight == 50).ToList();
        RightClick(cards[0]);
        await Wait(300);
        menu = OpenMenu();
        if (menu != null) { Shot(menu, "l05-ctx"); menu.IsOpen = false; }

        lw.Q.Text = "exa";
        await Wait(200);
        Shot(lw, "l06-search");
        lw.Q.Text = "";

        lw.UrlIn.Text = "not a url";
        Call(lw, "AddLink");
        await Wait(100);
        Shot(lw, "l07-bad");
        lw.UrlIn.Text = "example.org/docs";
        lw.NameIn.Text = "";
        Call(lw, "AddLink");
        await Wait(150);
        Shot(lw, "l08-added");
        log.Add("links added:  " + LinkOrder(app));

        Call(lw, "NewGroup");
        await Wait(200);
        Shot(lw, "l09-new-group");
        var ren = Find<TextBox>(lw.ListPanel).FirstOrDefault();
        if (ren != null) { ren.Text = "참고 자료"; Keyboard.Focus(lw.NameIn); }
        await Wait(150);
        log.Add("links newgrp: " + LinkOrder(app) + " target=" + app.Settings.LinksTarget);

        // 좁은 창과 영어
        lw.Width = 340; lw.Height = 540;
        w.Width = 360; w.Height = 640;
        await Wait(300);
        Shot(lw, "l10-narrow");
        Shot(w, "m11-narrow");
        app.SetLang("en");
        await Wait(300);
        Shot(w, "m12-en");
        Shot(lw, "l11-en");
        app.SetLang("ko");
        lw.Close();

        // 트레이: 최소화하면 숨고 트레이 아이콘이 생기며, 되살리면 아이콘이 사라진다
        w.WindowState = WindowState.Minimized;
        await Wait(300);
        object? TrayField() => typeof(MainWindow).GetField("tray", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(w);
        log.Add($"tray after minimize: visible={w.IsVisible} tray={(TrayField() != null)}");
        w.ShowFromTray();
        await Wait(300);
        log.Add($"tray after restore: visible={w.IsVisible} state={w.WindowState} tray={(TrayField() != null)}");

        // 대화상자 - ShowDialog 가 막아도 이 흐름은 안쪽 메시지 루프에서 이어진다
        _ = w.Dispatcher.BeginInvoke(() => Call(w, "OpenLinkSettings"));
        await Wait(600);
        var dlg = Application.Current.Windows.OfType<LinkSettingsWindow>().FirstOrDefault();
        if (dlg != null) { Shot(dlg, "m13-link-settings"); dlg.Close(); }
        await Wait(300);
    }
}
#endif
