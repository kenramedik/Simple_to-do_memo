using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using System.Windows.Threading;
using SimpleToDoMemo.Core;
using static SimpleToDoMemo.Views.Ui;

namespace SimpleToDoMemo.Views;

public partial class MainWindow : Window
{
    const int PinPerPage = 3;

    readonly App app;
    Memos memos => app.Memos;
    Settings settings => app.Settings;

    string today = Memos.Key(DateTime.Today);
    string cur = Memos.Key(DateTime.Today);
    DateTime calMonth;
    int pinPage, pinPages = 1;
    bool searchOpen;
    Tray? tray;

    readonly Button linksBtn, sortBtn, searchBtn, todayBtn, prevBtn, nextBtn;

    public MainWindow(App app)
    {
        this.app = app;
        InitializeComponent();
        calMonth = FirstOfMonth(cur);

        linksBtn = IconButton("IconBtn", Ico.Link, 15, 2.1);
        sortBtn = new Button { Style = S("IconBtn") };
        searchBtn = IconButton("IconBtn", Ico.Search, 15, 2.2);
        todayBtn = new Button { Style = S("Chip") };
        prevBtn = IconButton("IconBtn", Ico.ChevL, 15, 2.4);
        nextBtn = IconButton("IconBtn", Ico.ChevR, 15, 2.4);
        foreach (var b in new[] { linksBtn, sortBtn, searchBtn, todayBtn, prevBtn, nextBtn })
        {
            if (Actions.Children.Count > 0) b.Margin = new Thickness(6, 0, 0, 0);
            Actions.Children.Add(b);
        }

        MonthChev.Content = Icon(Ico.ChevD, 11, 3);
        CalPrev.Content = Icon(Ico.ChevL, 14, 2.4);
        CalNext.Content = Icon(Ico.ChevR, 14, 2.4);
        CalClose.Content = Icon(Ico.Cross, 14, 2.4);
        SearchIcon.Content = Icon(Ico.Search, 15, 2.2);
        SearchClose.Content = Icon(Ico.Cross, 14, 2.4);
        PinPrev.Content = Icon(Ico.ChevL, 13, 2.6);
        PinNext.Content = Icon(Ico.ChevR, 13, 2.6);
        AddBtn.Content = Icon(Ico.Arrow, 15, 2.6);

        Wire();
        RestoreSavedBounds();
        Topmost = settings.AlwaysOnTop;
        Render();
        Loaded += (_, _) => Input.Focus();
    }

    /* ─── 연결 ─── */
    void Wire()
    {
        MonthBtn.Click += (_, _) => ToggleCal();
        prevBtn.Click += (_, _) => Goto(Memos.Shift(cur, -1));
        nextBtn.Click += (_, _) => Goto(Memos.Shift(cur, 1));
        todayBtn.Click += (_, _) => { CheckRollover(); Goto(today); };
        sortBtn.Click += (_, _) => { settings.NewestFirst = !settings.NewestFirst; app.SaveSettings(); Render(); };
        searchBtn.Click += (_, _) => { if (searchOpen) CloseSearch(); else OpenSearch(); };
        linksBtn.Click += (_, _) => app.OpenLinks();
        CalPrev.Click += (_, _) => { calMonth = calMonth.AddMonths(-1); RenderCal(); };
        CalNext.Click += (_, _) => { calMonth = calMonth.AddMonths(1); RenderCal(); };
        CalClose.Click += (_, _) => ToggleCal();
        PinPrev.Click += (_, _) => { pinPage--; RenderPinned(); };
        PinNext.Click += (_, _) => { pinPage++; RenderPinned(); };
        SearchClose.Click += (_, _) => CloseSearch();
        SearchInput.TextChanged += (_, _) => { if (searchOpen) RenderList(); };

        AddBtn.Click += (_, _) => AddItem();
        Input.TextChanged += (_, _) => AddBtn.IsEnabled = Input.Text.Trim().Length > 0;
        Input.KeyDown += (_, e) => { if (e.Key == Key.Enter) { AddItem(); e.Handled = true; } };

        // 고정 패널 위에서 휠을 굴리면 페이지를 넘긴다. 한 번에 여러 장이 넘어가지 않게 짧은 쿨다운을 둔다.
        int wheelAt = 0;
        Pinned.PreviewMouseWheel += (_, e) =>
        {
            if (pinPages < 2) return;
            e.Handled = true;
            if (Environment.TickCount - wheelAt < 220) return;
            int next = Math.Clamp(pinPage + (e.Delta < 0 ? 1 : -1), 0, pinPages - 1);
            if (next == pinPage) return;
            wheelAt = Environment.TickCount;
            pinPage = next;
            RenderPinned();
        };

        MLinks.Click += (_, _) => app.OpenLinks();
        MImport.Click += (_, _) => app.ImportFromElectron(this, ask: true);
        MExit.Click += (_, _) => app.Quit();
        MTop.Click += (_, _) => app.SetAlwaysOnTop(!settings.AlwaysOnTop);
        MTray.Click += (_, _) => { settings.MinimizeToTray = !settings.MinimizeToTray; app.SaveSettings(); ApplyMenuState(); };
        MLock.Click += (_, _) => app.SetDeleteLock(!settings.DeleteLock);
        MLinkSettings.Click += (_, _) => OpenLinkSettings();
        MKo.Click += (_, _) => app.SetLang("ko");
        MEn.Click += (_, _) => app.SetLang("en");
        MAbout.Click += (_, _) => app.ShowAbout(this);

        PreviewKeyDown += OnPreviewKeyDown;
        Activated += (_, _) => { CheckRollover(); FocusInput(); };
        StateChanged += OnStateChanged;
        Closing += OnClosing;
        DayTitle.SizeChanged += (_, _) => FitDow();

        // 창을 켜둔 채 자정을 넘기면 '오늘'이 어제가 된다. 절전에서 깨어난 경우도 있어 주기적으로 본다.
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
        timer.Tick += (_, _) => CheckRollover();
        timer.Start();
        Microsoft.Win32.SystemEvents.PowerModeChanged += (_, e) =>
        {
            if (e.Mode == Microsoft.Win32.PowerModes.Resume) Dispatcher.BeginInvoke(CheckRollover);
        };

        ListScroll.PreviewMouseMove += OnDragMove;
        ListScroll.PreviewMouseLeftButtonUp += OnDragUp;
        // 다른 창으로 넘어가는 등 붙잡은 마우스를 놓치면 끌기를 취소한다 (붙잡지 못했던 경우는 제외)
        ListScroll.LostMouseCapture += (_, _) => { if (drag != null && hadCapture) EndDrag(false); };
    }

    /* ─── 그리기 ─── */
    public void Render()
    {
        var T = L.Cur;
        Title = $"{L.Plain(T.AppTitle)}  v{App.Version}";
        MFile.Header = T.MenuFile; MSettings.Header = T.MenuSettings; MHelp.Header = T.MenuHelp;
        MLinks.Header = T.OpenLinks; MImport.Header = T.ImportElectron; MExit.Header = T.Exit;
        MTop.Header = T.AlwaysOnTop; MTray.Header = T.MinToTray; MLock.Header = T.DeleteLock;
        MLinkSettings.Header = T.LinkSettings; MAbout.Header = T.About;
        ApplyMenuState();

        Tip(MonthBtn, Calendar.Visibility == Visibility.Visible ? T.CloseCalendar : T.OpenCalendar);
        Tip(linksBtn, T.LinksTip);
        Tip(searchBtn, T.SearchTip);
        Tip(prevBtn, T.PrevDay);
        Tip(nextBtn, T.NextDay);
        Tip(CalPrev, T.PrevMonth);
        Tip(CalNext, T.NextMonth);
        Tip(CalClose, T.CloseCalendar);
        Tip(PinPrev, T.Prev);
        Tip(PinNext, T.Next);
        Tip(SearchClose, T.Close);
        Tip(AddBtn, T.AddTip);
        SearchInput.Tag = T.SearchPlaceholder;
        Input.Tag = T.AddPlaceholder;
        todayBtn.Content = T.Today;
        // 방향에 따라 아이콘도 같이 바뀐다
        sortBtn.Content = Icon(settings.NewestFirst ? Ico.SortDesc : Ico.SortAsc, 15, 2.1);
        Tip(sortBtn, settings.NewestFirst ? T.SortDesc : T.SortAsc);

        RenderHeader();
        RenderPinned();
        RenderList();
        if (Calendar.Visibility == Visibility.Visible) RenderCal();
        tray?.Refresh();
    }

    public void ApplyMenuState()
    {
        MTop.IsChecked = settings.AlwaysOnTop;
        MTray.IsChecked = settings.MinimizeToTray;
        MLock.IsChecked = settings.DeleteLock;
        MKo.IsChecked = !L.Cur.English;
        MEn.IsChecked = L.Cur.English;
    }

    void RenderHeader()
    {
        var T = L.Cur;
        var d = Memos.Parse(cur);
        int w = (int)d.DayOfWeek;
        YmLabel.Text = T.Ym(d.Year, d.Month);
        DayNum.Text = T.DayNum(d.Day);
        DowRun.Foreground = w == 0 ? B("Red") : w == 6 ? B("Blue") : B("Text2");
        FitDow();

        todayBtn.IsEnabled = cur != today;
        bool isToday = cur == today;
        Root.Background = isToday ? B("TodayBg") : B("Bg");
        Footer.Background = isToday ? Brushes.Transparent : B("Bg");
        TodayWash.Visibility = isToday ? Visibility.Visible : Visibility.Collapsed;

        var arr = memos.ItemsFor(cur, today, settings.NewestFirst);
        int settled = arr.Count(e => e.Item.State != ItemState.Open);
        Progress.Visibility = arr.Count > 0 ? Visibility.Visible : Visibility.Hidden;
        ProgText.Text = arr.Count > 0 ? $"{settled}/{arr.Count}" : "";
        Track.UpdateLayout();
        void SetFill() => Fill.Width = arr.Count > 0 ? Track.ActualWidth * settled / arr.Count : 0;
        if (Track.ActualWidth > 0) SetFill(); else Track.Loaded += (_, _) => SetFill();
        Track.SizeChanged -= TrackResized;
        Track.SizeChanged += TrackResized;
        void TrackResized(object? s, SizeChangedEventArgs e) => SetFill();
    }

    /* 창이 좁으면 긴 요일 이름(Wednesday)이 오른쪽 버튼을 밀어낸다.
       넘칠 때만 짧은 이름으로 바꾸고, 버튼이 여섯 개라 최소 폭에서는 짧은 이름(Wed)도 모자라면 요일을 뺀다.
       한글은 '수요일'이 짧아 대개 그대로 남는다. */
    void FitDow()
    {
        var T = L.Cur;
        int w = (int)Memos.Parse(cur).DayOfWeek;
        double avail = DayTitle.ActualWidth;
        string pick = "  " + T.DowLong[w];
        if (avail > 0)
        {
            double Width(string dow)
            {
                var probe = new TextBlock { FontFamily = DayTitle.FontFamily };
                probe.Inlines.Add(new Run(DayNum.Text) { FontSize = 25, FontWeight = FontWeights.Bold });
                probe.Inlines.Add(new Run(dow) { FontSize = 15, FontWeight = FontWeights.Medium });
                probe.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                return probe.DesiredSize.Width;
            }
            if (Width(pick) > avail + 0.5) pick = "  " + T.DowShort[w];
            if (Width(pick) > avail + 0.5) pick = "";
        }
        DowRun.Text = pick;
    }

    static DateTime FirstOfMonth(string k) { var d = Memos.Parse(k); return new DateTime(d.Year, d.Month, 1); }
    string DateLabel(string k)
    {
        var d = Memos.Parse(k);
        return L.Cur.Full(d.Year, d.Month, d.Day, L.Cur.DowLong[(int)d.DayOfWeek]);
    }
    string SrcLabel(string k) { var d = Memos.Parse(k); return L.Cur.Md(d.Month, d.Day); }

    /* ─── 항목 행 ─── */
    sealed class Row
    {
        public TodoItem Item = null!;
        public string From = "";
        public bool Carried;
        public int Index;
        public Grid Host = null!;
        public Border Card = null!;
        public Border Box = null!;
        public TextBlock Txt = null!;
        public FrameworkElement? Grip;
        public Rectangle? Dash;
        public Rectangle LineAbove = null!, LineBelow = null!;
        public bool Hover;
    }

    readonly List<Row> rows = new();

    void RenderList()
    {
        ListPanel.Children.Clear();
        rows.Clear();
        EmptyHost.Content = null;
        var T = L.Cur;

        if (searchOpen) { RenderSearch(); return; }

        var arr = memos.ItemsFor(cur, today, settings.NewestFirst);
        if (arr.Count == 0)
        {
            EmptyHost.Content = Empty(Ico.Calendar, T.EmptyTitle, T.EmptySub);
            return;
        }
        foreach (var e in arr)
        {
            var row = BuildRow(e);
            rows.Add(row);
            if (ListPanel.Children.Count > 0) row.Host.Margin = new Thickness(0, 6, 0, 0);
            ListPanel.Children.Add(row.Host);
        }
    }

    Row BuildRow(Entry e)
    {
        var T = L.Cur;
        var it = e.Item;
        var r = new Row { Item = it, From = e.From, Carried = e.Carried, Index = e.Index };

        var grid = new Grid { Margin = new Thickness(e.Carried ? 12 : 8, 9, 8, 9), MinHeight = 26 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        if (!e.Carried)
        {
            var grip = new Border { Child = Grip(), Background = Brushes.Transparent, Width = 14, Margin = new Thickness(0, 0, 3, 0),
                                    Opacity = 0, Cursor = Cursors.SizeAll, VerticalAlignment = VerticalAlignment.Stretch };
            ((FrameworkElement)grip.Child).VerticalAlignment = VerticalAlignment.Center;
            ((FrameworkElement)grip.Child).HorizontalAlignment = HorizontalAlignment.Center;
            TextElement.SetForeground(grip, B("Text3"));
            Tip(grip, T.DragTip);
            grid.Children.Add(grip);
            r.Grip = grip;
        }

        r.Box = MakeBox(it, 20, 7);
        r.Box.Margin = new Thickness(0, 0, 11, 0);
        Grid.SetColumn(r.Box, 1);
        grid.Children.Add(r.Box);

        r.Txt = new TextBlock { FontSize = 14, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center };
        FillText(r.Txt, it, null);
        Grid.SetColumn(r.Txt, 2);
        grid.Children.Add(r.Txt);
        TipIfTrimmed(r.Txt, it.Text);

        if (e.Carried)
        {
            var chip = CarryChip(e.From);
            Grid.SetColumn(chip, 3);
            grid.Children.Add(chip);
        }
        if (it.PinnedAt != null)
        {
            var pin = new Border { Child = Icon(Ico.Pin, 14, 1.6, fill: true), Margin = new Thickness(8, 0, 0, 0),
                                   Background = Brushes.Transparent, VerticalAlignment = VerticalAlignment.Center };
            TextElement.SetForeground(pin, B("Accent"));
            Tip(pin, T.Unpin);
            Grid.SetColumn(pin, 4);
            grid.Children.Add(pin);
        }

        var inner = new Grid();
        var color = ColorOf(it.Color);
        if (color is Color c)
        {
            var bar = new Border { Width = 5, HorizontalAlignment = HorizontalAlignment.Left, Background = new SolidColorBrush(c),
                                   CornerRadius = new CornerRadius(10, 0, 0, 10) };
            inner.Children.Add(bar);
        }
        inner.Children.Add(grid);

        r.Card = new Border { CornerRadius = new CornerRadius(11), BorderThickness = new Thickness(1), MinHeight = 46,
                              Child = inner, Cursor = Cursors.Hand };
        if (e.Carried)
        {
            // WPF 의 Border 는 점선을 못 그린다 - 둥근 사각형을 위에 겹쳐 점선 테두리를 만든다
            r.Dash = new Rectangle { RadiusX = 11, RadiusY = 11, StrokeThickness = 1, StrokeDashArray = new DoubleCollection { 4, 3 },
                                     IsHitTestVisible = false, Stroke = B("BorderStrong") };
        }

        r.LineAbove = DropLine(VerticalAlignment.Top);
        r.LineBelow = DropLine(VerticalAlignment.Bottom);
        r.Host = new Grid();
        r.Host.Children.Add(r.Card);
        if (r.Dash != null) r.Host.Children.Add(r.Dash);
        r.Host.Children.Add(r.LineAbove);
        r.Host.Children.Add(r.LineBelow);

        PaintRow(r);
        r.Card.MouseEnter += (_, _) => { r.Hover = true; PaintRow(r); };
        r.Card.MouseLeave += (_, _) => { r.Hover = false; PaintRow(r); };

        bool pressed = false;
        r.Card.MouseLeftButtonDown += (_, ev) =>
        {
            if (r.Txt.Parent == null) return;   // 수정 중
            pressed = true;
            if (!r.Carried) BeginDragCandidate(r, ev.GetPosition(ListPanel));
        };
        r.Card.MouseLeftButtonUp += (_, ev) =>
        {
            if (!pressed) return;
            pressed = false;
            if (justDragged) return;
            memos.Cycle(it, cur);
            Render();
        };
        r.Card.MouseLeave += (_, _) => { if (drag == null) pressed = false; };
        r.Card.MouseRightButtonUp += (_, ev) => { ev.Handled = true; ItemMenu(r, ev.GetPosition(Root)); };
        return r;
    }

    static Rectangle DropLine(VerticalAlignment va) => new()
    {
        Height = 2, RadiusX = 1, RadiusY = 1, Fill = B("Accent"), VerticalAlignment = va,
        Margin = va == VerticalAlignment.Top ? new Thickness(2, -5, 2, 0) : new Thickness(2, 0, 2, -5),
        Visibility = Visibility.Collapsed, IsHitTestVisible = false,
    };

    void PaintRow(Row r)
    {
        var it = r.Item;
        bool done = it.State == ItemState.Done, dropped = it.State == ItemState.Dropped, settled = done || dropped;
        var color = ColorOf(it.Color);
        Brush bg, border;
        Effect? fx;
        if (settled)
        {
            bg = r.Hover ? (color is Color c1 && done ? Mix(c1, .11) : B("SurfaceHover")) : Brushes.Transparent;
            border = r.Hover ? B("Border") : Brushes.Transparent;
            fx = null;
        }
        else
        {
            bg = color is Color c2 ? Mix(c2, r.Hover ? .17 : .12) : B("Surface");
            border = r.Hover ? B("BorderStrong") : B("Border");
            fx = r.Hover ? (Effect)R("ShadowMd") : (Effect)R("ShadowSm");
        }
        if (r.Carried)
        {
            r.Card.BorderBrush = Brushes.Transparent;
            if (r.Dash != null) r.Dash.Stroke = settled && !r.Hover ? Brushes.Transparent : (r.Hover ? B("BorderStrong") : B("Border"));
            fx = r.Hover && !settled ? (Effect)R("ShadowSm") : null;
        }
        else r.Card.BorderBrush = border;
        r.Card.Background = bg;
        r.Card.Effect = fx;
        if (r.Grip != null && drag?.Row != r) r.Grip.Opacity = r.Hover ? 0.8 : 0;
        PaintBox(r.Box, it, r.Hover);
    }

    // 체크 상자: 미확인 = 빈 칸, 완료 = 채운 칸 + 체크, 드랍 = 회색 칸 + X
    Border MakeBox(TodoItem it, double size, double radius)
    {
        var box = new Border { Width = size, Height = size, CornerRadius = new CornerRadius(radius),
                               BorderThickness = new Thickness(1.5), VerticalAlignment = VerticalAlignment.Center };
        box.Child = it.State == ItemState.Dropped ? Icon(Ico.Cross, 11, 3.6) : Icon(Ico.Check, 12, 3.4);
        ((FrameworkElement)box.Child).HorizontalAlignment = HorizontalAlignment.Center;
        ((FrameworkElement)box.Child).VerticalAlignment = VerticalAlignment.Center;
        PaintBox(box, it, false);
        return box;
    }

    static void PaintBox(Border box, TodoItem it, bool hover, Brush? idle = null)
    {
        var color = ColorOf(it.Color);
        Brush accent = color is Color c ? new SolidColorBrush(c) : B("Accent");
        Brush ink = it.Color != null && Ink.TryGetValue(it.Color, out var hex) ? Hex(hex) : Brushes.White;
        if (it.State == ItemState.Done)
        {
            box.Background = accent; box.BorderBrush = accent; TextElement.SetForeground(box, ink);
        }
        else if (it.State == ItemState.Dropped)
        {
            box.Background = B("Text3"); box.BorderBrush = B("Text3"); TextElement.SetForeground(box, Brushes.White);
        }
        else
        {
            box.Background = Brushes.Transparent;
            box.BorderBrush = hover ? accent : idle ?? B("BorderStrong");
            TextElement.SetForeground(box, Brushes.Transparent);
        }
    }

    /* 괄호 안 숫자 링크 + 검색어 강조. 일치 구간이 링크 경계에 걸쳐도 조각별로 나눠 강조한다. */
    void FillText(TextBlock tb, TodoItem it, string? q)
    {
        var text = it.Text;
        tb.Inlines.Clear();
        bool done = it.State == ItemState.Done, dropped = it.State == ItemState.Dropped;
        tb.Foreground = done || dropped ? B("Text3") : B("Text");
        tb.Opacity = dropped ? 0.72 : 1;
        if (done || dropped) tb.TextDecorations = TextDecorations.Strikethrough;

        int hi = string.IsNullOrEmpty(q) ? -1 : text.IndexOf(q, StringComparison.OrdinalIgnoreCase);
        int hiEnd = hi < 0 ? -1 : hi + q!.Length;

        void Put(InlineCollection parent, int from, int to)
        {
            int a = Math.Max(from, hi), b = Math.Min(to, hiEnd);
            if (hi < 0 || a >= b) { if (to > from) parent.Add(new Run(text[from..to])); return; }
            if (a > from) parent.Add(new Run(text[from..a]));
            parent.Add(new Run(text[a..b]) { Background = B("Mark") });
            if (to > b) parent.Add(new Run(text[b..to]));
        }

        int pos = 0;
        var link = settings.Link;
        if (!string.IsNullOrEmpty(link.Url))
        {
            foreach (System.Text.RegularExpressions.Match m in LinkUtil.NumberId.Matches(text))
            {
                int start = m.Index + 1, end = start + m.Groups[1].Length;
                var href = LinkUtil.Build(link.Url, link.Param, m.Groups[1].Value);
                Put(tb.Inlines, pos, start);
                var h = new Hyperlink { Cursor = Cursors.Hand, Focusable = false };
                if (done || dropped) { h.Foreground = tb.Foreground; h.TextDecorations = TextDecorations.Strikethrough; }
                else { h.Foreground = B("Accent"); h.TextDecorations = TextDecorations.Underline; }
                Put(h.Inlines, start, end);
                h.Click += (_, _) => LinkUtil.OpenInBrowser(href);
                h.ToolTip = href;
                ToolTipService.SetInitialShowDelay(h, 250);
                tb.Inlines.Add(h);
                pos = end;
            }
        }
        Put(tb.Inlines, pos, text.Length);
    }

    // 말줄임된 것만 전체 내용을 툴팁으로 보여준다
    static void TipIfTrimmed(TextBlock tb, string full)
    {
        void Check()
        {
            if (tb.ActualWidth <= 0) return;
            var probe = new TextBlock { FontSize = tb.FontSize, FontFamily = tb.FontFamily, FontWeight = tb.FontWeight, Text = full };
            probe.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Tip(tb, probe.DesiredSize.Width > tb.ActualWidth + 1 ? full : null);
        }
        tb.SizeChanged += (_, _) => Check();
    }

    Button CarryChip(string from)
    {
        var sp = new StackPanel { Orientation = Orientation.Horizontal };
        sp.Children.Add(Icon(Ico.Carry, 10, 2.6));
        sp.Children.Add(new TextBlock { Text = SrcLabel(from), Margin = new Thickness(3, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center });
        var chip = new Button { Style = S("BaseButton"), Content = sp, FontSize = 10, FontWeight = FontWeights.SemiBold,
                                Padding = new Thickness(6, 2, 6, 2), BorderThickness = new Thickness(1), Margin = new Thickness(8, 0, 0, 0),
                                Background = B("SurfaceHover"), BorderBrush = B("Border"), Foreground = B("Text3"),
                                VerticalAlignment = VerticalAlignment.Center };
        SetRadius(chip, new CornerRadius(99));
        chip.MouseEnter += (_, _) => { chip.Background = B("AccentSoft"); chip.BorderBrush = B("AccentLine"); chip.Foreground = B("Accent"); };
        chip.MouseLeave += (_, _) => { chip.Background = B("SurfaceHover"); chip.BorderBrush = B("Border"); chip.Foreground = B("Text3"); };
        Tip(chip, L.Cur.CarriedFrom(DateLabel(from)));
        // 행으로 전달되면 완료 처리가 돼 항목이 사라진다
        chip.PreviewMouseLeftButtonDown += (_, e) => e.Handled = true;
        chip.PreviewMouseLeftButtonUp += (_, e) => { e.Handled = true; Goto(from); };
        return chip;
    }

    /* ─── 우클릭 메뉴 ─── */
    void ItemMenu(Row r, Point at)
    {
        var T = L.Cur;
        var it = r.Item;
        var menu = new ContextMenu();
        menu.Items.Add(Item(T.Edit, () => StartEdit(r)));
        menu.Items.Add(Item(it.PinnedAt != null ? T.Unpin : T.Pin, () =>
        {
            it.PinnedAt = it.PinnedAt != null ? null : DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            memos.Save(); Render();
        }));
        menu.Items.Add(Item(it.Color != null && Ui.Colors.ContainsKey(it.Color) ? T.ColorIs(T.ColorName(it.Color)) : T.SetColor,
            () => OpenColorPop(at, it)));
        if (!settings.DeleteLock)
        {
            menu.Items.Add(new Separator());
            menu.Items.Add(Item(T.Del, () => { memos.Delete(r.From, it); Render(); }, "danger"));
        }
        menu.PlacementTarget = r.Card;
        menu.Closed += (_, _) => FocusInput();
        menu.IsOpen = true;
    }

    void OpenColorPop(Point at, TodoItem it)
    {
        var T = L.Cur;
        ColorGrid.Children.Clear();
        void Add(string key, Color? c, string name)
        {
            var sw = new Grid { Width = 26, Height = 26, Margin = new Thickness(3.5), Cursor = Cursors.Hand, Background = Brushes.Transparent };
            bool on = (it.Color ?? "") == key;
            if (on) sw.Children.Add(new Ellipse { Width = 33, Height = 33, Margin = new Thickness(-3.5), Stroke = B("Text2"), StrokeThickness = 1.5 });
            var dot = new Ellipse { Width = 26, Height = 26 };
            if (c is Color cc) dot.Fill = new SolidColorBrush(cc);
            else
            {
                dot.Fill = B("Surface"); dot.Stroke = B("BorderStrong"); dot.StrokeThickness = 1.5;
            }
            sw.Children.Add(dot);
            if (c == null)
                sw.Children.Add(new Line { X1 = 7, Y1 = 19, X2 = 19, Y2 = 7, Stroke = B("Red"), StrokeThickness = 1.5, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round });
            var scale = new ScaleTransform(1, 1, 13, 13);
            dot.RenderTransform = scale;
            sw.MouseEnter += (_, _) => { scale.ScaleX = scale.ScaleY = 1.13; };
            sw.MouseLeave += (_, _) => { scale.ScaleX = scale.ScaleY = 1; };
            Tip(sw, name);
            sw.MouseLeftButtonUp += (_, _) =>
            {
                it.Color = key == "" ? null : key;
                memos.Save();
                ColorPop.IsOpen = false;
                Render();
            };
            ColorGrid.Children.Add(sw);
        }
        Add("", null, T.NoColor);
        foreach (var (k, hex) in Ui.Colors) Add(k, (Color)ColorConverter.ConvertFromString(hex), T.ColorName(k));
        ColorPop.HorizontalOffset = at.X - 8;
        ColorPop.VerticalOffset = at.Y;
        ColorPop.IsOpen = true;
    }

    void StartEdit(Row r)
    {
        var it = r.Item;
        var grid = (Grid)r.Txt.Parent;
        var inp = new TextBox { Style = S("BareInput"), Text = it.Text, FontSize = 14, Padding = new Thickness(0) };
        Grid.SetColumn(inp, 2);
        grid.Children.Remove(r.Txt);
        grid.Children.Add(inp);
        r.Card.Cursor = Cursors.IBeam;
        inp.Focus();
        inp.SelectAll();

        bool closed = false;
        void Commit()
        {
            if (closed) return;
            closed = true;
            var v = inp.Text.Trim();
            if (v.Length > 0) it.Text = v;
            memos.Save();
            Render();
            FocusInput();
        }
        inp.LostKeyboardFocus += (_, _) => Commit();
        inp.PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter) { e.Handled = true; Commit(); }
            else if (e.Key == Key.Escape) { e.Handled = true; closed = true; Render(); FocusInput(); }
        };
        inp.PreviewMouseLeftButtonDown += (_, e) => e.Handled = false;
    }

    /* ─── 드래그 정렬 ─── 넘어온 항목은 원래 날짜의 순서를 따르므로 끌어 옮기지 않는다 */
    sealed class DragState { public Row Row = null!; public Point Start; public bool Moved; }
    DragState? drag;
    int? dropTo;
    bool justDragged;
    DispatcherTimer? autoScroll;
    bool hadCapture;
    double lastY;

    void BeginDragCandidate(Row r, Point p) => drag = new DragState { Row = r, Start = p };

    void OnDragMove(object sender, MouseEventArgs e)
    {
        if (drag == null) return;
        if (e.LeftButton != MouseButtonState.Pressed) { EndDrag(commit: false); return; }
        DragTo(e.GetPosition(ListPanel), e.GetPosition(ListScroll).Y);
    }

    // 끌고 있는 위치 p(목록 좌표)와 스크롤 영역 안의 높이 - 테스트에서도 이 함수를 직접 부른다
    internal void DragTo(Point p, double yInScroll)
    {
        if (drag == null) return;
        lastY = yInScroll;
        if (!drag.Moved)
        {
            if (Math.Abs(p.Y - drag.Start.Y) < 4) return;
            drag.Moved = true;
            drag.Row.Host.Opacity = 0.35;
            Mouse.OverrideCursor = Cursors.SizeAll;
            hadCapture = ListScroll.CaptureMouse();
            autoScroll = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
            autoScroll.Tick += (_, _) =>
            {
                double h = ListScroll.ActualHeight;
                int dir = lastY < 26 ? -1 : lastY > h - 26 ? 1 : 0;
                if (dir == 0) return;
                ListScroll.ScrollToVerticalOffset(ListScroll.VerticalOffset + dir * 7);
                UpdateDrop(Mouse.GetPosition(ListPanel).Y);
            };
            autoScroll.Start();
        }
        UpdateDrop(p.Y);
    }

    void UpdateDrop(double y)
    {
        ClearDropMarks();
        dropTo = null;
        if (drag == null) return;
        var own = rows.Where(r => !r.Carried).ToList();
        if (own.Count == 0) return;
        // 내림차순이면 화면 순서가 data[cur] 의 역순이다. 끼워 넣을 자리도 뒤집어야 한다.
        int ToData(int v) => settings.NewestFirst ? own.Count - v : v;
        bool InPlace(int to) => to == drag.Row.Index || to == drag.Row.Index + 1;
        double Top(Row r) => r.Host.TranslatePoint(new Point(0, 0), ListPanel).Y;

        for (int i = 0; i < own.Count; i++)
        {
            double top = Top(own[i]), h = own[i].Host.ActualHeight;
            if (y < top || y > top + h) continue;
            bool below = y - top > h / 2;
            int to = ToData(i + (below ? 1 : 0));
            if (InPlace(to)) return;
            dropTo = to;
            (below ? own[i].LineBelow : own[i].LineAbove).Visibility = Visibility.Visible;
            return;
        }
        if (y < Top(own[0]))
        {
            int to = ToData(0);
            if (!InPlace(to)) { dropTo = to; own[0].LineAbove.Visibility = Visibility.Visible; }
        }
        else if (y > Top(own[^1]) + own[^1].Host.ActualHeight)
        {
            int to = ToData(own.Count);
            if (!InPlace(to)) { dropTo = to; own[^1].LineBelow.Visibility = Visibility.Visible; }
        }
    }

    void ClearDropMarks()
    {
        foreach (var r in rows) { r.LineAbove.Visibility = Visibility.Collapsed; r.LineBelow.Visibility = Visibility.Collapsed; }
    }

    void OnDragUp(object sender, MouseButtonEventArgs e)
    {
        if (drag == null) return;
        if (!drag.Moved) { drag = null; return; }
        e.Handled = true;
        EndDrag(commit: true);
    }

    void EndDrag(bool commit)
    {
        var d = drag;
        drag = null;
        autoScroll?.Stop();
        autoScroll = null;
        Mouse.OverrideCursor = null;
        hadCapture = false;
        if (ListScroll.IsMouseCaptured) ListScroll.ReleaseMouseCapture();
        if (d == null || !d.Moved) return;
        justDragged = true;
        Dispatcher.BeginInvoke(() => justDragged = false, DispatcherPriority.Input);
        if (commit && dropTo is int to) memos.Move(cur, d.Row.Index, to);
        dropTo = null;
        Render();
    }

    /* ─── 검색 ─── */
    void OpenSearch()
    {
        searchOpen = true;
        SearchBar.Visibility = Visibility.Visible;
        if (Calendar.Visibility == Visibility.Visible) ToggleCal();
        Render();
        SearchInput.Focus();
        SearchInput.SelectAll();
    }

    void CloseSearch()
    {
        searchOpen = false;
        SearchBar.Visibility = Visibility.Collapsed;
        SearchInput.Text = "";
        Render();
        Input.Focus();
    }

    void RenderSearch()
    {
        var T = L.Cur;
        var q = SearchInput.Text.Trim();
        if (q.Length == 0)
        {
            SearchCount.Text = "";
            EmptyHost.Content = Empty(Ico.Search, T.SearchTitle, T.SearchSub);
            return;
        }
        var hits = memos.Search(q, settings.NewestFirst);
        SearchCount.Text = T.Hits(hits.Count);
        if (hits.Count == 0) { EmptyHost.Content = Empty("", T.NoHitTitle, T.NoHitSub); return; }

        foreach (var (date, it) in hits)
        {
            var d = Memos.Parse(date);
            var grid = new Grid { Margin = new Thickness(12, 9, 12, 9) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var box = MakeBox(it, 20, 7);
            box.Margin = new Thickness(0, 0, 11, 0);
            box.Cursor = Cursors.Hand;
            grid.Children.Add(box);
            var txt = new TextBlock { FontSize = 14, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center };
            FillText(txt, it, q);
            Grid.SetColumn(txt, 1);
            grid.Children.Add(txt);
            var date2 = new TextBlock { Text = T.Md(d.Month, d.Day), FontSize = 11, FontWeight = FontWeights.SemiBold,
                                        Foreground = B("Text3"), Margin = new Thickness(11, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(date2, 2);
            grid.Children.Add(date2);

            var inner = new Grid();
            if (ColorOf(it.Color) is Color c)
                inner.Children.Add(new Border { Width = 5, HorizontalAlignment = HorizontalAlignment.Left, Background = new SolidColorBrush(c),
                                                CornerRadius = new CornerRadius(10, 0, 0, 10) });
            inner.Children.Add(grid);
            var card = new Border { CornerRadius = new CornerRadius(11), BorderThickness = new Thickness(1), MinHeight = 46,
                                    Background = B("Surface"), BorderBrush = B("Border"), Effect = (Effect)R("ShadowSm"),
                                    Child = inner, Cursor = Cursors.Hand };
            if (ListPanel.Children.Count > 0) card.Margin = new Thickness(0, 6, 0, 0);
            Tip(card, $"{DateLabel(date)} · {T.ClickToOpen}");
            card.MouseEnter += (_, _) => { card.BorderBrush = B("BorderStrong"); card.Effect = (Effect)R("ShadowMd"); };
            card.MouseLeave += (_, _) => { card.BorderBrush = B("Border"); card.Effect = (Effect)R("ShadowSm"); };
            box.MouseLeftButtonUp += (_, e) => { e.Handled = true; memos.Cycle(it, cur); Render(); };
            box.MouseLeftButtonDown += (_, e) => e.Handled = true;
            card.MouseLeftButtonUp += (_, _) => { CloseSearch(); Goto(date); };
            ListPanel.Children.Add(card);
        }
    }

    /* ─── 고정 항목 ─── */
    void RenderPinned()
    {
        var T = L.Cur;
        var pins = memos.Pinned();
        if (pins.Count == 0 || searchOpen)
        {
            Pinned.Visibility = Visibility.Collapsed;
            if (pins.Count == 0) { pinPage = 0; pinPages = 1; }
            return;
        }
        Pinned.Visibility = Visibility.Visible;
        int pages = pinPages = (pins.Count + PinPerPage - 1) / PinPerPage;
        pinPage = Math.Clamp(pinPage, 0, pages - 1);
        PinTitle.Text = T.Pinned(pins.Count);
        PinPager.Visibility = pages < 2 ? Visibility.Collapsed : Visibility.Visible;
        PinPage.Text = $"{pinPage + 1}/{pages}";
        PinPrev.IsEnabled = pinPage > 0;
        PinNext.IsEnabled = pinPage < pages - 1;

        PinList.Children.Clear();
        foreach (var (date, it) in pins.Skip(pinPage * PinPerPage).Take(PinPerPage))
        {
            var grid = new Grid { Margin = new Thickness(7, 4, 3, 4), MinHeight = 26 };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var box = MakeBox(it, 17, 6);
            box.Margin = new Thickness(0, 0, 9, 0);
            if (it.State == ItemState.Open) box.BorderBrush = B("AccentLine");
            grid.Children.Add(box);

            var txt = new TextBlock { FontSize = 13, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center };
            FillText(txt, it, null);
            Grid.SetColumn(txt, 1);
            grid.Children.Add(txt);

            var act = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(6, 0, 0, 0) };
            var go = IconButton("FlatBtn", Ico.Jump, 14, 2.1);
            var off = new Button { Style = S("FlatBtn"), Content = Icon(Ico.Pin, 14, 1.6, fill: true) };
            foreach (var b in new[] { go, off })
            {
                b.Width = b.Height = 25;
                SetRadius(b, new CornerRadius(6));
                b.Foreground = B("Text3");
                b.MouseEnter += (_, _) => { b.Background = Brushes.White; b.Foreground = B("Accent"); };
                b.MouseLeave += (_, _) => { b.ClearValue(BackgroundProperty); b.Foreground = B("Text3"); };
                act.Children.Add(b);
            }
            Tip(go, T.GoTo(DateLabel(date)));
            Tip(off, T.Unpin);
            go.Click += (_, _) => Goto(date);
            off.Click += (_, _) => { it.PinnedAt = null; memos.Save(); Render(); };
            Grid.SetColumn(act, 2);
            grid.Children.Add(act);

            var inner = new Grid();
            if (ColorOf(it.Color) is Color c)
                inner.Children.Add(new Border { Width = 3, HorizontalAlignment = HorizontalAlignment.Left, Background = new SolidColorBrush(c),
                                                CornerRadius = new CornerRadius(8, 0, 0, 8) });
            inner.Children.Add(grid);
            var row = new Border { CornerRadius = new CornerRadius(8), Child = inner, Background = Brushes.Transparent, Cursor = Cursors.Hand };
            if (PinList.Children.Count > 0) row.Margin = new Thickness(0, 2, 0, 0);
            Tip(row, T.AddedOn(DateLabel(date)));
            var hoverBg = new SolidColorBrush(Color.FromArgb(0x9E, 255, 255, 255));
            row.MouseEnter += (_, _) => { row.Background = hoverBg; if (it.State == ItemState.Open) PaintBox(box, it, true); };
            row.MouseLeave += (_, _) => { row.Background = Brushes.Transparent; if (it.State == ItemState.Open) PaintBox(box, it, false, B("AccentLine")); };
            row.MouseLeftButtonUp += (_, _) => { memos.Cycle(it, cur); Render(); };
            PinList.Children.Add(row);
        }
    }

    /* ─── 달력 ─── */
    void ToggleCal()
    {
        bool show = Calendar.Visibility != Visibility.Visible;
        Calendar.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        MonthBtn.Foreground = show ? B("Accent") : B("Text2");
        MonthBtn.Background = show ? B("AccentSoft") : Brushes.Transparent;
        MonthChev.RenderTransform = new RotateTransform(show ? 180 : 0);
        Tip(MonthBtn, show ? L.Cur.CloseCalendar : L.Cur.OpenCalendar);
        if (show) { calMonth = FirstOfMonth(cur); RenderCal(); }
    }

    void RenderCal()
    {
        var T = L.Cur;
        CalLabel.Text = T.Ym(calMonth.Year, calMonth.Month);
        CalGrid.Children.Clear();
        for (int i = 0; i < 7; i++)
        {
            CalGrid.Children.Add(new TextBlock
            {
                Text = T.Dow[i], FontSize = 10.5, FontWeight = FontWeights.SemiBold, Height = 22, Padding = new Thickness(0, 4, 0, 0),
                TextAlignment = TextAlignment.Center,
                Foreground = i == 0 ? B("Red") : i == 6 ? B("Blue") : B("Text3"),
            });
        }
        int start = (int)calMonth.DayOfWeek;
        int last = DateTime.DaysInMonth(calMonth.Year, calMonth.Month);
        for (int i = 0; i < start; i++) CalGrid.Children.Add(new Border());

        for (int d = 1; d <= last; d++)
        {
            var k = Memos.Key(new DateTime(calMonth.Year, calMonth.Month, d));
            var arr = memos.ItemsFor(k, today, false);
            int col = (start + d - 1) % 7;
            var holiday = Holidays.Name(k, T.English);
            bool isSel = k == cur, isToday = k == today;

            var sp = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
            var num = new TextBlock
            {
                Text = d.ToString(), FontSize = 12.5, HorizontalAlignment = HorizontalAlignment.Center,
                FontWeight = isSel || isToday ? FontWeights.Bold : FontWeights.Medium,
                Foreground = isSel ? Brushes.White : holiday != null || col == 0 ? B("Red") : col == 6 ? B("Blue") : B("Text"),
            };
            sp.Children.Add(num);
            if (arr.Count > 0)
            {
                bool allDone = arr.All(e => e.Item.State != ItemState.Open);
                var cnt = new Border
                {
                    MinWidth = 15, Height = 14, Padding = new Thickness(4, 0, 4, 0), Margin = new Thickness(0, 2, 0, 0),
                    CornerRadius = new CornerRadius(7), HorizontalAlignment = HorizontalAlignment.Center,
                    Background = isSel ? new SolidColorBrush(Color.FromArgb(0x40, 255, 255, 255)) : allDone ? B("Border") : B("AccentSoft"),
                    Child = new TextBlock
                    {
                        Text = arr.Count.ToString(), FontSize = 9.5, FontWeight = FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center,
                        Foreground = isSel ? Brushes.White : allDone ? B("Text3") : B("Accent"),
                    },
                };
                sp.Children.Add(cnt);
            }

            Brush idleBg = isSel ? B("Accent") : isToday ? B("AccentSoft") : holiday != null ? B("Holiday") : Brushes.Transparent;
            Brush hoverBg = isSel ? B("AccentHover") : holiday != null ? B("HolidayHover") : isToday ? B("AccentSoft") : B("SurfaceHover");
            var cell = new Border
            {
                Height = 40, Margin = new Thickness(1), CornerRadius = new CornerRadius(9), Child = sp, Cursor = Cursors.Hand,
                Background = idleBg, BorderThickness = new Thickness(isToday && !isSel ? 1.5 : 1),
                BorderBrush = isToday && !isSel ? B("Accent") : Brushes.Transparent,
            };
            cell.MouseEnter += (_, _) =>
            {
                cell.Background = hoverBg;
                if (!isToday && !isSel && holiday == null) cell.BorderBrush = B("Border");
            };
            cell.MouseLeave += (_, _) =>
            {
                cell.Background = idleBg;
                if (!isToday && !isSel) cell.BorderBrush = Brushes.Transparent;
            };
            var tip = string.Join("  ·  ", new[]
            {
                holiday,
                arr.Count > 0 ? T.CountTip(arr.Count, arr.Count(e => e.Item.State != ItemState.Open)) : null,
            }.Where(s => s != null));
            Tip(cell, tip);
            var key = k;
            cell.MouseLeftButtonUp += (_, _) => { cur = key; Render(); };
            CalGrid.Children.Add(cell);
        }
    }

    /* ─── 동작 ─── */
    void AddItem()
    {
        var v = Input.Text.Trim();
        if (v.Length == 0) return;
        memos.Add(cur, v);
        Input.Text = "";
        Render();
        // 새 항목이 들어간 쪽으로 스크롤 - 내림차순이면 맨 위
        if (settings.NewestFirst) ListScroll.ScrollToTop(); else ListScroll.ScrollToBottom();
    }

    public void Goto(string k)
    {
        cur = k;
        calMonth = FirstOfMonth(cur);
        Render();
    }

    void CheckRollover()
    {
        var now = Memos.Key(DateTime.Today);
        if (now == today) return;
        bool wasToday = cur == today;
        today = now;
        if (wasToday) { cur = now; calMonth = FirstOfMonth(cur); }
        Render();
    }

    // 창이 활성화돼 있으면 어디를 클릭했든 타이핑은 입력칸으로 간다.
    // 버튼과 행은 포커스를 받지 않게 해 두어 입력칸이 포커스를 잃지 않는다.
    void FocusInput()
    {
        if (Keyboard.FocusedElement is TextBox tb && tb.IsVisible) return;
        (searchOpen ? SearchInput : Input).Focus();
    }

    void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        var mods = Keyboard.Modifiers;
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (mods == ModifierKeys.Control)
        {
            switch (key)
            {
                case Key.F: e.Handled = true; if (searchOpen) SearchInput.SelectAll(); else OpenSearch(); return;
                case Key.T: e.Handled = true; app.SetAlwaysOnTop(!settings.AlwaysOnTop); return;
                case Key.Q: e.Handled = true; app.Quit(); return;
                case Key.L: e.Handled = true; app.OpenLinks(); return;
            }
        }
        if (mods != ModifierKeys.None) return;
        if (key == Key.Escape)
        {
            if (ColorPop.IsOpen) ColorPop.IsOpen = false;
            else if (searchOpen) CloseSearch();
            else if (Calendar.Visibility == Visibility.Visible) ToggleCal();
            else return;
            e.Handled = true;
            return;
        }
        if (searchOpen) return;
        if (key != Key.Left && key != Key.Right) return;
        // 입력칸이 비어 있을 때만 좌우 방향키로 날짜를 옮긴다 (글자가 있으면 커서 이동)
        if (Keyboard.FocusedElement is TextBox tb && (tb != Input || tb.Text.Length > 0)) return;
        e.Handled = true;
        Goto(Memos.Shift(cur, key == Key.Left ? -1 : 1));
    }

    void OpenLinkSettings()
    {
        var dlg = new LinkSettingsWindow(settings.Link) { Owner = this };
        if (dlg.ShowDialog() == true)
        {
            settings.Link = dlg.Result;
            app.SaveSettings();
            Render();
        }
        FocusInput();
    }

    /* ─── 트레이 ─── */
    void OnStateChanged(object? sender, EventArgs e)
    {
        if (WindowState == WindowState.Minimized && settings.MinimizeToTray)
        {
            tray ??= new Tray(app, this);
            Hide();
        }
    }

    // 트레이는 창을 되살린 뒤에 없앤다 - 먼저 지우면 복원 실패 시 앱에 접근할 길이 사라진다
    public void ShowFromTray()
    {
        Show();
        if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
        Activate();
        Topmost = !Topmost; Topmost = settings.AlwaysOnTop;   // 다른 창 뒤에 숨지 않게 한 번 앞으로
        tray?.Dispose();
        tray = null;
    }

    /* ─── 창 위치 ─── */
    void RestoreSavedBounds()
    {
        var b = settings.Bounds;
        if (b == null || b.Width < MinWidth || b.Height < MinHeight) { WindowStartupLocation = WindowStartupLocation.CenterScreen; return; }
        // 모니터를 뺐거나 해상도가 바뀌어 창이 화면 밖에 뜨지 않게 한다
        var vs = new Rect(SystemParameters.VirtualScreenLeft, SystemParameters.VirtualScreenTop,
                          SystemParameters.VirtualScreenWidth, SystemParameters.VirtualScreenHeight);
        if (!vs.IntersectsWith(new Rect(b.X + 40, b.Y + 10, Math.Max(1, b.Width - 80), 30)))
        {
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Width = b.Width; Height = b.Height;
            return;
        }
        WindowStartupLocation = WindowStartupLocation.Manual;
        Left = b.X; Top = b.Y; Width = b.Width; Height = b.Height;
    }

    void OnClosing(object? sender, CancelEventArgs e)
    {
        var rb = WindowState == WindowState.Normal ? new Rect(Left, Top, ActualWidth, ActualHeight) : RestoreBounds;
        if (!rb.IsEmpty) settings.Bounds = new Bounds { X = rb.X, Y = rb.Y, Width = rb.Width, Height = rb.Height };
        app.SaveSettings();
        tray?.Dispose();
    }
}
