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

    // 할 일 카드 - 그룹 묶음 안에 들어 있을 수 있어 목록의 직계 자식으로 찾지 않는다
    static List<Border> Cards(Panel list) => Find<Border>(list).Where(b => b.MinHeight == 46).ToList();
    static Border Card(Panel list, int i) => Cards(list)[i];

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
        // 가장자리 자동 스크롤은 실제 마우스 위치를 읽어 흉내 낸 위치를 덮는다 - 테스트에서는 멈춘다
        (F("autoScroll") as System.Windows.Threading.DispatcherTimer)?.Stop();
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
        // 가져오기만 확인하는 모드 - 실행만으로는 아무것도 가져오지 않는지 본 뒤, 메뉴와 같은 경로로 가져온다
        if (Environment.GetEnvironmentVariable("SIMPLETODOMEMO_TEST_MODE") == "import")
        {
            await Wait(400);
            log.Add($"before import: dates={app.Memos.Data.Count} links={app.Links.Sum(g => g.Links.Count)}");
            foreach (var lang in new[] { "ko", "en" })
            {
                app.SetLang(lang);
                w.MFile.IsSubmenuOpen = true;
                await Wait(300);
                if (Find<Popup>(w.MFile).FirstOrDefault()?.Child is FrameworkElement fm) Shot(fm, "i00-file-menu-" + lang);
                w.MFile.IsSubmenuOpen = false;
            }
            app.SetLang("ko");
            app.ImportFromElectron(w, ask: false);
            await Wait(300);
            log.Add("memos: " + string.Join(" ; ", app.Memos.Data.OrderBy(p => p.Key).Select(p => p.Key + "=" + string.Join(",", p.Value.Select(i => $"{i.Text}[{i.State}{(i.Color != null ? "," + i.Color : "")}{(i.PinnedAt != null ? ",pin" : "")}]")))));
            log.Add("links: " + LinkOrder(app) + " collapsed=" + string.Join(",", app.Links.Select(g => g.Collapsed)));
            log.Add($"settings: lang={app.Settings.Lang} newestFirst={app.Settings.NewestFirst} link={app.Settings.Link.Url}?{app.Settings.Link.Param} deleteLock={app.Settings.DeleteLock} migrated={app.Settings.MigratedFromElectron}");
            Shot(w, "i01-imported");
            return;
        }
        if (Environment.GetEnvironmentVariable("SIMPLETODOMEMO_TEST_MODE") == "groups")
        {
            await Groups(app);
            return;
        }
        if (Environment.GetEnvironmentVariable("SIMPLETODOMEMO_TEST_MODE") == "carry")
        {
            await Carry(app);
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
        int n = Cards(w.ListPanel).Count;
        log.Add("rows " + n);
        log.Add("order before: " + Order(app));
        var last = (Grid)Card(w.ListPanel, n - 1).Parent;
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
        var cyc = Card(w.ListPanel, Cards(w.ListPanel).Count - 1);
        var states = new List<int>();
        for (int i = 0; i < 3; i++)
        {
            ClickEl(Card(w.ListPanel, Cards(w.ListPanel).Count - 1));
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

    /* ─── 할 일 그룹 ─── 오늘 날짜에 그룹 없는 항목 몇 개가 있는 자료로 시작한다 */
    static void PressKey(UIElement el, Key key)
    {
        var src = PresentationSource.FromVisual((Visual)el)!;
        el.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, src, 0, key) { RoutedEvent = Keyboard.PreviewKeyDownEvent });
    }
    // 오늘 할 일을 화면 순서대로 (넘어온 항목 포함, 그룹 이름 붙여서)
    static string Day(App app) => string.Join(" | ", app.Memos.ItemsFor(DateTime.Today.ToString("yyyy-MM-dd"), DateTime.Today.ToString("yyyy-MM-dd"), false)
        .Select(e => e.Item).Select(x => x.Text + (x.Group != null ? "@" + (app.Groups.Find(g => g.Id == x.Group)?.Name ?? "?" + x.Group) : "")));
    // 묶음 머리 (높이 30)
    static List<Border> Heads(MainWindow w) => Find<Border>(w.ListPanel).Where(b => b.Height == 30).ToList();
    static Border CardOf(MainWindow w, string text) =>
        Cards(w.ListPanel).First(c => Find<TextBlock>(c).Any(t => t.Inlines.OfType<System.Windows.Documents.Run>().Any(r => r.Text.StartsWith(text))));
    static TextBox Editor(MainWindow w) => Find<TextBox>(w.ListPanel).First();

    static async Task NameIt(MainWindow w, string name, Key key = System.Windows.Input.Key.Enter)
    {
        await Wait(150);
        Editor(w).Text = name;
        PressKey(Editor(w), key);
        await Wait(200);
    }

    /* ─── 넘어온 항목 옮기기 ─── 9/24·9/25 에 남은 할 일이 오늘로 넘어와 있는 자료로 시작한다 */
    static string Shown(App app, string day, bool newestFirst = false) => string.Join(" | ",
        app.Memos.ItemsFor(day, DateTime.Today.ToString("yyyy-MM-dd"), newestFirst)
            .Select(e => (e.Carried ? "*" : "") + e.Item.Text.Split(' ')[0] + (e.Item.Group != null ? "@" + app.Groups.Find(g => g.Id == e.Item.Group)?.Name : "")));

    static Point Below(MainWindow w, string text) { var c = CardOf(w, text); return c.TranslatePoint(new Point(c.ActualWidth / 2, c.ActualHeight * .85), w.ListPanel); }
    static Point Above(MainWindow w, string text) { var c = CardOf(w, text); return c.TranslatePoint(new Point(c.ActualWidth / 2, 6), w.ListPanel); }

    static async Task Carry(App app)
    {
        var w = app.MainWin;
        w.Height = 1000;
        await Wait(500);
        var today = DateTime.Today.ToString("yyyy-MM-dd");
        var next = SimpleToDoMemo.Core.Memos.AddBizDays(today, 1);
        log.Add("start:        " + Shown(app, today));
        Shot(w, "c01-start");

        // 넘어온 항목을 오늘 항목 사이로
        await Drag(w, w.ListScroll, w.ListPanel, CardOf(w, "지난주"), Below(w, "코드"), "c02-drag-carried");
        log.Add("carried down: " + Shown(app, today));
        // 오늘 항목을 넘어온 항목 위로
        await Drag(w, w.ListScroll, w.ListPanel, CardOf(w, "운동"), Above(w, "목요일"), "c03-drag-own-up");
        log.Add("own up:       " + Shown(app, today));
        log.Add("next day:     " + Shown(app, next));
        log.Add("dates kept:   " + string.Join(", ", app.Memos.Data.OrderBy(p => p.Key).Select(p => p.Key + "=" + string.Join("/", p.Value.Select(i => i.Text.Split(' ')[0])))));
        Shot(w, "c04-after");

        // 넘어온 항목을 그룹으로 - 머리 위에 놓기, 그룹 안 행 사이에 놓기
        Call(w, "StartNewGroup", new object[] { null! });
        await NameIt(w, "업무");
        w.Input.Text = "분기 보고서 작성";
        Call(w, "AddItem");
        w.Input.Text = "고객사 메일 회신";
        Call(w, "AddItem");
        await Wait(200);
        var head = Heads(w).First(h => Find<TextBlock>(h).Any(t => t.Text == "업무"));
        await Drag(w, w.ListScroll, w.ListPanel, CardOf(w, "목요일"), head.TranslatePoint(new Point(head.ActualWidth / 2, 15), w.ListPanel), "c05-carried-into-group");
        log.Add("into group:   " + Shown(app, today));
        await Drag(w, w.ListScroll, w.ListPanel, CardOf(w, "지난주"), Above(w, "고객사"), "c06-carried-between");
        log.Add("between:      " + Shown(app, today));
        log.Add("next day:     " + Shown(app, next));
        Shot(w, "c07-grouped");

        // 내림차순에서 옮기기 - 화면이 뒤집혀 있어도 놓은 자리에 들어가야 한다
        app.Settings.NewestFirst = true;
        w.Render();
        await Wait(200);
        log.Add("desc before:  " + Shown(app, today, true));
        await Drag(w, w.ListScroll, w.ListPanel, CardOf(w, "회의"), Below(w, "운동"), "c08-desc-drag");
        log.Add("desc after:   " + Shown(app, today, true));
        Shot(w, "c09-desc");
        app.Settings.NewestFirst = false;
        w.Render();
        log.Add("memos.json has order: " + File.ReadAllText(Path.Combine(SimpleToDoMemo.Core.Storage.Dir, "memos.json")).Contains("\"order\""));
    }

    static async Task Groups(App app)
    {
        var w = app.MainWin;
        await Wait(500);
        Shot(w, "g01-no-groups");

        // 그룹이 없을 때는 예전처럼 순서만 바꾼다 - '회의 자료'를 '운동' 아래로, 다시 제자리로
        var ex = CardOf(w, "운동");
        await Drag(w, w.ListScroll, w.ListPanel, CardOf(w, "회의 자료"), ex.TranslatePoint(new Point(ex.ActualWidth / 2, ex.ActualHeight * .85), w.ListPanel), "g00-bare-drag");
        log.Add("bare drag: " + Day(app));
        var mt = CardOf(w, "장보기");
        await Drag(w, w.ListScroll, w.ListPanel, CardOf(w, "회의 자료"), mt.TranslatePoint(new Point(mt.ActualWidth / 2, 6), w.ListPanel), "g00-bare-drag2");
        log.Add("bare back: " + Day(app));

        Call(w, "StartNewGroup", new object[] { null! });
        await Wait(150);
        Editor(w).Text = "업무";
        Shot(w, "g02-editor");
        PressKey(Editor(w), System.Windows.Input.Key.Enter);
        await Wait(200);
        log.Add($"after 업무: groups={string.Join(",", app.Groups.Select(g => g.Name))} target={app.Groups.Find(g => g.Id == app.Settings.MemoGroup)?.Name}");

        w.Input.Text = "분기 보고서 작성";
        Call(w, "AddItem");
        w.Input.Text = "고객사 메일 회신 (123456)";
        Call(w, "AddItem");
        await Wait(150);

        // Esc 로 새 그룹 만들기를 그만두면 아무것도 생기지 않는다
        Call(w, "StartNewGroup", new object[] { null! });
        await NameIt(w, "버릴 그룹", System.Windows.Input.Key.Escape);
        log.Add($"after esc: groups={string.Join(",", app.Groups.Select(g => g.Name))}");

        Call(w, "StartNewGroup", new object[] { null! });
        await NameIt(w, "개인");
        w.Input.Text = "헬스장 등록";
        Call(w, "AddItem");
        await Wait(200);
        w.ListScroll.ScrollToTop();
        await Wait(100);
        Shot(w, "g03-grouped");
        log.Add("day: " + Day(app));
        log.Add($"heads={Heads(w).Count} cards={Cards(w.ListPanel).Count}");

        // 그룹 없는 할 일을 우클릭 메뉴로 업무에
        RightClick(CardOf(w, "회의 자료"));
        await Wait(300);
        var menu = OpenMenu();
        if (menu != null)
        {
            Shot(menu, "g04-item-menu");
            var to = menu.Items.OfType<MenuItem>().First(m => (m.Header as string) == "업무");
            to.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            menu.IsOpen = false;
        }
        await Wait(200);
        log.Add("menu move: " + Day(app));

        // 끝에 가까우면 자동 스크롤이 실제 마우스 위치를 읽어 흉내 낸 위치를 덮는다 - 창을 키워 둔다
        w.Height = 1000;
        await Wait(200);
        // 끌어서 다른 그룹으로 - '운동'을 개인 묶음 머리 위에 놓는다
        var personalHead = Heads(w).Last();
        var p = personalHead.TranslatePoint(new Point(personalHead.ActualWidth / 2, personalHead.ActualHeight / 2), w.ListPanel);
        await Drag(w, w.ListScroll, w.ListPanel, CardOf(w, "운동"), p, "g05-drag-into");
        log.Add("drag into: " + Day(app));

        // 끌어서 다른 그룹의 행 사이로 - '코드 리뷰'를 업무의 '고객사 메일' 위에
        var mail = CardOf(w, "고객사 메일");
        p = mail.TranslatePoint(new Point(mail.ActualWidth / 2, 6), w.ListPanel);
        await Drag(w, w.ListScroll, w.ListPanel, CardOf(w, "코드 리뷰"), p, "g06-drag-between");
        log.Add("drag between: " + Day(app));
        w.ListScroll.ScrollToTop();
        await Wait(100);
        Shot(w, "g07-after-drag");

        // 추가할 그룹 메뉴, 묶음 머리 메뉴
        Call(w, "TargetMenu");
        await Wait(300);
        menu = OpenMenu();
        if (menu != null) { Shot(menu, "g08-target-menu"); menu.IsOpen = false; }
        // 메뉴가 다 닫힐 때 입력칸으로 포커스를 돌린다 - 그 전에 다음 메뉴를 열면 그 포커스 이동에 곧바로 닫힌다
        await Wait(600);
        var workHead = Heads(w).First(h => Find<TextBlock>(h).Any(t => t.Text == "업무"));
        Hover(workHead);
        await Wait(100);
        Shot(w, "g09-head-hover");
        Unhover(workHead);
        RightClick(workHead);
        await Wait(300);
        menu = OpenMenu();
        log.Add("head menu open: " + (menu != null));
        if (menu != null) { Shot(menu, "g10-head-menu"); menu.IsOpen = false; }

        // 접기, 순서 바꾸기, 이름 바꾸기
        Call(w, "ToggleFold", app.Groups.First(g => g.Name == "업무"));
        await Wait(200);
        Shot(w, "g11-folded");
        Call(w, "MoveGroup", 0, 1);
        await Wait(150);
        log.Add($"moved group: {string.Join(",", app.Groups.Select(g => g.Name))} collapsed={string.Join(",", app.Groups.Select(g => g.Collapsed))}");
        ClickEl(Heads(w).First(h => Find<TextBlock>(h).Any(t => t.Text == "업무")));   // 머리 누르면 다시 펼침
        await Wait(200);
        var secs = (System.Collections.IList)typeof(MainWindow).GetField("sections", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(w)!;
        var personalSec = secs.Cast<object>().First(s => (s.GetType().GetField("G")!.GetValue(s) as SimpleToDoMemo.Core.MemoGroup)?.Name == "개인");
        Call(w, "StartRename", personalSec, app.Groups.First(g => g.Name == "개인"));
        await NameIt(w, "개인 생활과 건강 관리 관련 일들");
        w.ListScroll.ScrollToTop();
        await Wait(100);
        Shot(w, "g12-renamed");

        w.Width = 360; w.Height = 640;
        await Wait(300);
        Shot(w, "g13-narrow");
        app.SetLang("en");
        await Wait(300);
        Shot(w, "g14-en");
        app.SetLang("ko");
        w.Width = 460; w.Height = 740;

        // 그룹 삭제 - 할 일은 남고 그룹 없음으로
        var work = app.Groups.First(g => g.Name == "업무");
        Call(w, "DeleteGroup", work);
        await Wait(200);
        log.Add($"after delete: groups={string.Join(",", app.Groups.Select(g => g.Name))} target={app.Settings.MemoGroup ?? "null"}");
        log.Add("day: " + Day(app));
        Shot(w, "g15-deleted");
        log.Add("groups.json: " + File.ReadAllText(Path.Combine(SimpleToDoMemo.Core.Storage.Dir, "groups.json")).Replace("\r\n", " "));
    }
}
#endif
