using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using System.Windows.Threading;
using SimpleToDoMemo.Core;
using static SimpleToDoMemo.Views.Ui;

namespace SimpleToDoMemo.Views;

/* 링크 모음. 맨 앞 그룹은 늘 '그룹 없음' 칸이다 - 이름을 바꾸거나 지울 수 없고,
   그룹을 지우면 그 링크가 여기로 온다. */
public partial class LinksWindow : Window
{
    readonly App app;
    List<LinkGroup> groups => app.Links;
    Settings settings => app.Settings;

    public LinksWindow(App app)
    {
        this.app = app;
        InitializeComponent();
        PlusIcon.Content = Icon(Ico.Plus, 13, 2.6);
        QIcon.Content = Icon(Ico.Search, 15, 2.2);
        QClear.Content = Icon(Ico.Cross, 14, 2.4);
        TargetChev.Content = Icon(Ico.ChevD, 10, 3);
        AddBtn.Content = Icon(Ico.Plus, 15, 2.6);

        Q.TextChanged += (_, _) => Render();
        QClear.Click += (_, _) => { Q.Text = ""; Q.Focus(); };
        NewGroupBtn.Click += (_, _) => NewGroup();
        TargetBtn.Click += (_, _) => TargetMenu();
        AddBtn.Click += (_, _) => AddLink();
        UrlIn.TextChanged += (_, _) => { CheckAdd(); ShowAddHint(false); };
        UrlIn.KeyDown += (_, e) => { if (e.Key == Key.Enter) { e.Handled = true; AddLink(); } };
        NameIn.KeyDown += (_, e) =>
        {
            if (e.Key != Key.Enter) return;
            e.Handled = true;
            // 이름칸에 주소를 붙여 넣고 바로 Enter 를 친 경우 - 주소칸으로 옮겨 준다
            var n = NameIn.Text.Trim();
            if (UrlIn.Text.Trim() == "" && (n.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || n.StartsWith("https://", StringComparison.OrdinalIgnoreCase)))
            {
                UrlIn.Text = n;
                NameIn.Text = "";
            }
            if (UrlIn.Text.Trim() != "") AddLink(); else UrlIn.Focus();
        };

        PreviewKeyDown += OnPreviewKeyDown;
        ListScroll.PreviewMouseMove += OnDragMove;
        ListScroll.PreviewMouseLeftButtonUp += OnDragUp;
        // 다른 창으로 넘어가는 등 붙잡은 마우스를 놓치면 끌기를 취소한다 (붙잡지 못했던 경우는 제외)
        ListScroll.LostMouseCapture += (_, _) => { if (drag != null && hadCapture) EndDrag(false); };
        Closing += OnClosing;

        RestoreSavedBounds();
        Render();
        Loaded += (_, _) => NameIn.Focus();
    }

    const string Inbox = Storage.Inbox;
    LinkGroup GroupById(string id) => groups.FirstOrDefault(g => g.Id == id) ?? groups[0];
    bool HasUserGroups => groups.Count > 1;
    string GroupName(LinkGroup g) => g.Id == Inbox ? L.Cur.Ungrouped : g.Name;
    // 메뉴 글자의 _ 는 단축키 표시로 먹히므로 두 번 적는다
    static string MenuText(string s) => s.Replace("_", "__");

    void SetTarget(string id)
    {
        settings.LinksTarget = groups.Any(g => g.Id == id) ? id : Inbox;
        app.SaveSettings();
    }

    public void Reload() => Render();

    /* ─── 그리기 ─── */
    string Query => Q.Text.Trim();

    static bool Matches(LinkItem lk, string s) =>
        lk.Name.Contains(s, StringComparison.OrdinalIgnoreCase) || lk.Url.Contains(s, StringComparison.OrdinalIgnoreCase)
        || LinkUtil.Short(lk.Url).Contains(s, StringComparison.OrdinalIgnoreCase);

    sealed class LinkRow
    {
        public LinkItem Link = null!;
        public int Gi, Li;
        public Grid Host = null!;
        public Border Card = null!;
        public FrameworkElement Grip = null!, Open = null!;
        public Rectangle Above = null!, Below = null!;
    }
    sealed class Section
    {
        public int Gi;
        public StackPanel Panel = null!;
        public Border? Head;
        public Border? EmptyBox;
        public Rectangle? EmptyDash;
        public TextBlock? EmptyText;
        public bool Collapsed;
        public List<LinkRow> Rows = new();
    }
    readonly List<Section> sections = new();

    public void Render()
    {
        var T = L.Cur;
        Title = T.LinksTitle;
        TitleRun.Text = T.LinksTitle;
        var total = groups.Sum(g => g.Links.Count);
        TotalRun.Text = total > 0 ? "  " + T.LinksTotal(total) : "";
        NewGroupLabel.Text = T.NewGroup;
        Q.Tag = T.LinksSearch;
        Tip(QClear, T.Clear);
        NameIn.Tag = T.NamePh;
        UrlIn.Tag = T.UrlPh;
        Tip(AddBtn, T.LinkAddTip);
        TargetLabel.Text = T.AddTo;
        AddHint.Text = T.BadUrl;
        if (!groups.Any(g => g.Id == settings.LinksTarget)) SetTarget(Inbox);

        var s = Query;
        QClear.Visibility = Q.Text.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
        TargetRow.Visibility = HasUserGroups ? Visibility.Visible : Visibility.Collapsed;
        TargetName.Text = GroupName(GroupById(settings.LinksTarget));

        double scroll = ListScroll.VerticalOffset;
        ListPanel.Children.Clear();
        sections.Clear();
        EmptyHost.Content = null;

        if (total == 0 && !HasUserGroups)
        {
            EmptyHost.Content = Empty(Ico.Link, T.LinksEmptyTitle, T.LinksEmptySub);
            QCount.Text = "";
            return;
        }

        int hits = 0;
        for (int gi = 0; gi < groups.Count; gi++)
        {
            var g = groups[gi];
            var shown = s == "" ? g.Links : g.Links.Where(lk => Matches(lk, s)).ToList();
            hits += shown.Count;
            // 검색 중에는 걸린 링크가 있는 그룹만 보여준다
            if (s != "" && shown.Count == 0) continue;
            // 그룹을 만들지 않았으면 '그룹 없음' 머리 없이 링크만 늘어놓는다
            bool bare = g.Id == Inbox && !HasUserGroups;
            // 비어 있는 '그룹 없음' 은 자리만 차지하므로 숨긴다 - 끌어 옮길 때만 드러낸다
            if (g.Id == Inbox && !bare && g.Links.Count == 0) continue;
            var sec = BuildSection(g, gi, shown, s, bare);
            sections.Add(sec);
            ListPanel.Children.Add(sec.Panel);
        }
        QCount.Text = s != "" ? T.Hits(hits) : "";
        if (s != "" && hits == 0) EmptyHost.Content = Empty(Ico.Search, T.NoHitTitle, T.NoHitSub);
        ListScroll.UpdateLayout();
        ListScroll.ScrollToVerticalOffset(scroll);
    }

    Section BuildSection(LinkGroup g, int gi, List<LinkItem> shown, string s, bool bare)
    {
        var T = L.Cur;
        bool collapsed = g.Collapsed && s == "";
        bool isTarget = g.Id == settings.LinksTarget && HasUserGroups;
        var sec = new Section { Gi = gi, Collapsed = collapsed };
        // 그룹 사이를 margin 대신 padding 으로 띄운다 - 끌어 옮길 때 그룹 사이 틈에서도 놓을 자리를 잃지 않는다
        sec.Panel = new StackPanel { Background = Brushes.Transparent };
        var body = new StackPanel { Margin = new Thickness(0, 0, 0, 14) };

        if (!bare)
        {
            var fold = new ContentControl { Content = Icon(Ico.ChevD, 11, 3), Width = 16, Focusable = false, VerticalAlignment = VerticalAlignment.Center,
                                            RenderTransformOrigin = new Point(.5, .5), RenderTransform = new RotateTransform(collapsed ? -90 : 0) };
            var name = new TextBlock
            {
                Text = GroupName(g), FontSize = 12, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis,
                FontWeight = g.Id == Inbox ? FontWeights.SemiBold : FontWeights.Bold,
                Foreground = isTarget ? B("Accent") : g.Id == Inbox ? B("Text3") : B("Text2"),
                Margin = new Thickness(6, 0, 6, 0),
            };
            var cnt = new Border
            {
                MinWidth = 18, Height = 16, Padding = new Thickness(5, 0, 5, 0), CornerRadius = new CornerRadius(8), VerticalAlignment = VerticalAlignment.Center,
                Background = isTarget ? B("AccentSoft") : B("Border"),
                Child = new TextBlock
                {
                    Text = s != "" ? $"{shown.Count}/{g.Links.Count}" : g.Links.Count.ToString(), FontSize = 10, FontWeight = FontWeights.Bold,
                    Foreground = isTarget ? B("Accent") : B("Text2"), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
                },
            };
            TextElement.SetForeground(fold, B("Text3"));
            Tip(fold, collapsed ? T.Unfold : T.Fold);

            var dock = new DockPanel { LastChildFill = false };
            StackPanel? act = null;
            if (g.Id != Inbox)
            {
                act = new StackPanel { Orientation = Orientation.Horizontal, Opacity = 0 };
                var ren = IconButton("FlatBtn", Ico.Pen, 13, 2);
                var del = IconButton("FlatBtn", Ico.Trash, 13, 2);
                foreach (var b in new[] { ren, del })
                {
                    b.Width = b.Height = 24;
                    SetRadius(b, new CornerRadius(6));
                    b.Foreground = B("Text3");
                    act.Children.Add(b);
                }
                del.MouseEnter += (_, _) => del.Foreground = B("Red");
                del.MouseLeave += (_, _) => del.Foreground = B("Text3");
                Tip(ren, T.Rename);
                Tip(del, T.DelGroupTip);
                ren.Click += (_, _) => StartRename(sec, g);
                del.Click += (_, _) => DeleteGroup(g);
                DockPanel.SetDock(act, Dock.Right);
                dock.Children.Add(act);
                name.MouseLeftButtonDown += (_, e) => { if (e.ClickCount == 2) { e.Handled = true; StartRename(sec, g); } };
            }
            dock.Children.Add(fold);
            dock.Children.Add(name);
            dock.Children.Add(cnt);
            // 이름이 길면 말줄임되도록 남는 폭을 이름에 준다
            dock.SizeChanged += (_, _) =>
            {
                double rest = dock.ActualWidth - 16 - 12 - cnt.ActualWidth - (act?.ActualWidth ?? 0) - 4;
                name.MaxWidth = Math.Max(20, rest);
            };

            var head = new Border
            {
                Height = 30, Margin = new Thickness(-6, 0, -6, 4), Padding = new Thickness(6, 0, 4, 0), CornerRadius = new CornerRadius(8),
                Background = Brushes.Transparent, Child = dock, Cursor = Cursors.Hand, BorderThickness = new Thickness(1.5), BorderBrush = Brushes.Transparent,
            };
            head.MouseEnter += (_, _) => { if (!dragActive) head.Background = B("Hover"); if (act != null) act.Opacity = 1; };
            head.MouseLeave += (_, _) => { if (!dragActive) head.Background = Brushes.Transparent; if (act != null) act.Opacity = 0; };
            head.MouseLeftButtonUp += (_, e) =>
            {
                if (s != "" || e.OriginalSource is DependencyObject d && FindButton(d)) return;
                g.Collapsed = !g.Collapsed;
                app.SaveLinks();
                Render();
            };
            head.MouseRightButtonUp += (_, e) => { e.Handled = true; GroupMenu(sec, g, collapsed, head); };
            sec.Head = head;
            sec.Panel.Children.Add(head);
            TipIfTrimmed(name);
        }

        if (!collapsed)
        {
            if (shown.Count == 0)
            {
                sec.EmptyDash = new Rectangle { RadiusX = 11, RadiusY = 11, Stroke = B("BorderStrong"), StrokeThickness = 1.5,
                                                StrokeDashArray = new DoubleCollection { 3, 2.5 } };
                sec.EmptyText = new TextBlock { Text = T.DropHere, FontSize = 12, Foreground = B("Text3"),
                                                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
                var eg = new Grid { Height = 42 };
                eg.Children.Add(sec.EmptyDash);
                eg.Children.Add(sec.EmptyText);
                sec.EmptyBox = new Border { Child = eg, CornerRadius = new CornerRadius(11), Background = Brushes.Transparent };
                body.Children.Add(sec.EmptyBox);
            }
            foreach (var lk in shown)
            {
                var row = BuildRow(lk, gi, s);
                sec.Rows.Add(row);
                if (body.Children.Count > 0) row.Host.Margin = new Thickness(0, 6, 0, 0);
                body.Children.Add(row.Host);
            }
        }
        sec.Panel.Children.Add(body);
        return sec;
    }

    static bool FindButton(DependencyObject d)
    {
        for (var p = d; p != null; p = p is Visual || p is System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetParent(p) : LogicalTreeHelper.GetParent(p))
            if (p is Button) return true;
        return false;
    }

    LinkRow BuildRow(LinkItem lk, int gi, string s)
    {
        var T = L.Cur;
        var r = new LinkRow { Link = lk, Gi = gi, Li = groups[gi].Links.IndexOf(lk) };
        var grid = new Grid { Margin = new Thickness(8, 7, 8, 7) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var grip = new Border { Child = Grip(), Width = 14, Margin = new Thickness(0, 0, 0, 0), Opacity = 0, Background = Brushes.Transparent,
                                Cursor = Cursors.SizeAll, Visibility = s != "" ? Visibility.Hidden : Visibility.Visible };
        ((FrameworkElement)grip.Child).VerticalAlignment = VerticalAlignment.Center;
        ((FrameworkElement)grip.Child).HorizontalAlignment = HorizontalAlignment.Center;
        TextElement.SetForeground(grip, B("Text3"));
        Tip(grip, T.DragMoveTip);
        grid.Children.Add(grip);
        r.Grip = grip;

        // 첫 글자 - 이모지나 한글도 한 글자로 잘리도록 문자 단위(텍스트 요소)로 자른다
        var first = System.Globalization.StringInfo.GetNextTextElement(lk.Name.Trim());
        var ava = new Border
        {
            Width = 30, Height = 30, CornerRadius = new CornerRadius(8), Background = B("AccentSoft"), Margin = new Thickness(6, 0, 10, 0),
            Child = new TextBlock { Text = first == "" ? "?" : first.ToUpperInvariant(), FontSize = 13, FontWeight = FontWeights.Bold,
                                    Foreground = B("Accent"), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center },
        };
        Grid.SetColumn(ava, 1);
        grid.Children.Add(ava);

        var main = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        var name = new TextBlock { FontSize = 13.5, FontWeight = FontWeights.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis };
        Highlight(name, lk.Name, s);
        var url = new TextBlock { FontSize = 11.5, Foreground = B("Text3"), TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(0, 1, 0, 0) };
        Highlight(url, LinkUtil.Short(lk.Url), s);
        main.Children.Add(name);
        main.Children.Add(url);
        Grid.SetColumn(main, 2);
        grid.Children.Add(main);

        var open = new ContentControl { Content = Icon(Ico.Jump, 14, 2.1), Width = 28, Height = 28, Focusable = false, Opacity = 0,
                                        Foreground = B("Accent"), Margin = new Thickness(6, 0, 0, 0), IsHitTestVisible = false };
        open.HorizontalContentAlignment = HorizontalAlignment.Center;
        open.VerticalContentAlignment = VerticalAlignment.Center;
        Grid.SetColumn(open, 3);
        grid.Children.Add(open);
        r.Open = open;

        r.Card = new Border { CornerRadius = new CornerRadius(11), BorderThickness = new Thickness(1), MinHeight = 50, Child = grid,
                              Background = B("Surface"), BorderBrush = B("Border"), Effect = (Effect)R("ShadowSm"), Cursor = Cursors.Hand };
        r.Above = DropLine(VerticalAlignment.Top);
        r.Below = DropLine(VerticalAlignment.Bottom);
        r.Host = new Grid();
        r.Host.Children.Add(r.Card);
        r.Host.Children.Add(r.Above);
        r.Host.Children.Add(r.Below);

        r.Card.MouseEnter += (_, _) =>
        {
            if (dragActive) return;
            r.Card.BorderBrush = B("BorderStrong"); r.Card.Effect = (Effect)R("ShadowMd"); grip.Opacity = .8; open.Opacity = 1;
        };
        r.Card.MouseLeave += (_, _) =>
        {
            r.Card.BorderBrush = B("Border"); r.Card.Effect = (Effect)R("ShadowSm"); grip.Opacity = 0; open.Opacity = 0;
        };

        // 말줄임된 것만 툴팁으로 - 늘 띄우면 행마다 그룹 머리를 가린다
        r.Card.SizeChanged += (_, _) =>
        {
            bool nameCut = Trimmed(name, lk.Name), urlCut = Trimmed(url, LinkUtil.Short(lk.Url));
            Tip(r.Card, nameCut ? $"{lk.Name} · {LinkUtil.Short(lk.Url)}" : urlCut ? LinkUtil.Short(lk.Url) : null);
        };

        bool pressed = false;
        r.Card.MouseLeftButtonDown += (_, e) =>
        {
            pressed = true;
            // 검색 중에는 보이는 순서가 실제 순서와 달라 끌어 옮기지 않는다
            if (Query == "") drag = new DragState { Row = r, Start = e.GetPosition(ListPanel) };
        };
        r.Card.MouseLeftButtonUp += (_, _) =>
        {
            if (!pressed) return;
            pressed = false;
            if (justDragged) return;
            LinkUtil.OpenInBrowser(lk.Url);
        };
        r.Card.MouseLeave += (_, _) => { if (drag == null) pressed = false; };
        r.Card.MouseRightButtonUp += (_, e) => { e.Handled = true; LinkMenu(r); };
        return r;
    }

    static Rectangle DropLine(VerticalAlignment va) => new()
    {
        Height = 2, RadiusX = 1, RadiusY = 1, Fill = B("Accent"), VerticalAlignment = va,
        Margin = va == VerticalAlignment.Top ? new Thickness(2, -5, 2, 0) : new Thickness(2, 0, 2, -5),
        Visibility = Visibility.Collapsed, IsHitTestVisible = false,
    };

    // 처음 일치하는 부분만 강조한다
    static void Highlight(TextBlock tb, string text, string s)
    {
        tb.Inlines.Clear();
        int i = s == "" ? -1 : text.IndexOf(s, StringComparison.OrdinalIgnoreCase);
        if (i < 0) { tb.Inlines.Add(new Run(text)); return; }
        if (i > 0) tb.Inlines.Add(new Run(text[..i]));
        tb.Inlines.Add(new Run(text.Substring(i, s.Length)) { Background = B("Mark") });
        if (i + s.Length < text.Length) tb.Inlines.Add(new Run(text[(i + s.Length)..]));
    }

    static bool Trimmed(TextBlock tb, string full)
    {
        if (tb.ActualWidth <= 0) return false;
        var probe = new TextBlock { FontSize = tb.FontSize, FontFamily = tb.FontFamily, FontWeight = tb.FontWeight, Text = full };
        probe.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        return probe.DesiredSize.Width > tb.ActualWidth + 1;
    }

    static void TipIfTrimmed(TextBlock tb) =>
        tb.SizeChanged += (_, _) => Tip(tb, Trimmed(tb, tb.Text) ? tb.Text : null);

    /* ─── 메뉴 ─── */
    void LinkMenu(LinkRow r)
    {
        var T = L.Cur;
        var lk = r.Link;
        var menu = new ContextMenu();
        menu.Items.Add(Item(T.OpenInBrowser, () => LinkUtil.OpenInBrowser(lk.Url)));
        menu.Items.Add(Item(T.CopyUrl, () => { try { Clipboard.SetText(lk.Url); } catch { } }));
        menu.Items.Add(Item(T.EditLink, () => EditLink(lk)));
        if (HasUserGroups)
        {
            // 드래그 말고도 옮길 수 있게 - 지금 그룹을 빼고 나머지를 늘어놓는다
            menu.Items.Add(new Separator());
            menu.Items.Add(Heading(T.MoveTo));
            for (int to = 0; to < groups.Count; to++)
            {
                if (to == r.Gi) continue;
                int target = to;
                var mi = Item(MenuText(GroupName(groups[to])), () => MoveLink(r.Gi, groups[r.Gi].Links.IndexOf(lk), target, groups[target].Links.Count));
                mi.Padding = new Thickness(20, 0, 12, 0);
                menu.Items.Add(mi);
            }
        }
        if (!settings.DeleteLock)
        {
            menu.Items.Add(new Separator());
            menu.Items.Add(Item(T.Del, () => { groups[r.Gi].Links.Remove(lk); app.SaveLinks(); Render(); }, "danger"));
        }
        menu.PlacementTarget = r.Card;
        menu.IsOpen = true;
    }

    void GroupMenu(Section sec, LinkGroup g, bool collapsed, Border head)
    {
        var T = L.Cur;
        int ui = groups.IndexOf(g);
        var menu = new ContextMenu();
        if (g.Id != Inbox)
        {
            menu.Items.Add(Item(T.Rename, () => StartRename(sec, g)));
            menu.Items.Add(Item(T.MoveUp, () => MoveGroup(ui, -1), enabled: ui > 1));
            menu.Items.Add(Item(T.MoveDown, () => MoveGroup(ui, 1), enabled: ui < groups.Count - 1));
            menu.Items.Add(new Separator());
            menu.Items.Add(Item(T.DelGroup, () => DeleteGroup(g), "danger"));
        }
        else
        {
            menu.Items.Add(Item(collapsed ? T.Unfold : T.Fold, () => { g.Collapsed = !g.Collapsed; app.SaveLinks(); Render(); }));
        }
        menu.PlacementTarget = head;
        menu.IsOpen = true;
    }

    void TargetMenu()
    {
        var menu = new ContextMenu { PlacementTarget = TargetBtn, Placement = PlacementMode.Top, HorizontalOffset = -8, VerticalOffset = 4 };
        foreach (var g in groups)
        {
            var id = g.Id;
            menu.Items.Add(Item(MenuText(GroupName(g)), () => { SetTarget(id); Render(); NameIn.Focus(); }, id == settings.LinksTarget ? "on" : null));
        }
        menu.IsOpen = true;
    }

    /* ─── 추가 ─── */
    bool CheckAdd()
    {
        bool ok = LinkUtil.Normalize(UrlIn.Text) != null;
        AddBtn.IsEnabled = ok;
        return ok;
    }

    void ShowAddHint(bool on)
    {
        SetIsBad(AddRing, on);
        SetIsBad(AddField, on);
        AddHint.Visibility = on ? Visibility.Visible : Visibility.Collapsed;
    }

    void AddLink()
    {
        var url = LinkUtil.Normalize(UrlIn.Text);
        if (url == null) { if (UrlIn.Text.Trim() != "") ShowAddHint(true); UrlIn.Focus(); return; }
        var lk = new LinkItem { Id = Storage.NewId(), Name = NameIn.Text.Trim() is { Length: > 0 } n ? n : LinkUtil.DefaultName(url), Url = url };
        var g = GroupById(settings.LinksTarget);
        g.Links.Add(lk);
        g.Collapsed = false;
        NameIn.Text = UrlIn.Text = "";
        CheckAdd();
        ShowAddHint(false);
        // 검색에 걸리지 않아 새 링크가 안 보이는 일이 없도록 검색을 비운다
        Q.Text = "";
        app.SaveLinks();
        Render();
        Flash(lk, scrollInto: true);
        NameIn.Focus();
    }

    void Flash(LinkItem lk, bool scrollInto)
    {
        var row = sections.SelectMany(x => x.Rows).FirstOrDefault(x => x.Link == lk);
        if (row == null) return;
        if (scrollInto) row.Host.BringIntoView();
        // 새로 들어온 행을 잠깐 강조색 테두리로 알린다
        var ring = new Border { CornerRadius = new CornerRadius(13), BorderThickness = new Thickness(3), BorderBrush = B("AccentRing"),
                                Margin = new Thickness(-3), IsHitTestVisible = false };
        var edge = new Border { CornerRadius = new CornerRadius(11), BorderThickness = new Thickness(1), BorderBrush = B("Accent"), IsHitTestVisible = false };
        row.Host.Children.Add(ring);
        row.Host.Children.Add(edge);
        var anim = new System.Windows.Media.Animation.DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(800))
        { BeginTime = TimeSpan.FromMilliseconds(350) };
        ring.BeginAnimation(OpacityProperty, anim);
        edge.BeginAnimation(OpacityProperty, anim);
    }

    /* ─── 그룹 ─── */
    void NewGroup()
    {
        var g = new LinkGroup { Id = Storage.NewId(), Name = L.Cur.NewGroupName };
        groups.Add(g);
        app.SaveLinks();
        Q.Text = "";
        Render();
        var sec = sections.FirstOrDefault(x => x.Gi == groups.Count - 1);
        if (sec == null) return;
        StartRename(sec, g, isNew: true);
        // 새 그룹은 맨 끝에 붙는다 - 자리가 잡힌 뒤 끝까지 내려야 이름칸이 다 보인다
        Dispatcher.BeginInvoke(() => ListScroll.ScrollToEnd(), DispatcherPriority.Loaded);
    }

    void StartRename(Section sec, LinkGroup g, bool isNew = false)
    {
        if (sec.Head == null) return;
        var inp = new TextBox { Style = S("DlgInput"), Text = g.Name, Height = 24, FontSize = 12, FontWeight = FontWeights.Bold, Padding = new Thickness(5, 0, 5, 0),
                                Margin = new Thickness(3, 0, 0, 0) };
        var old = sec.Head.Child;
        var holder = new Grid { Margin = new Thickness(0, 0, 0, 0) };
        holder.Children.Add(inp);
        sec.Head.Child = holder;
        sec.Head.Cursor = Cursors.IBeam;
        inp.Focus();
        inp.SelectAll();

        bool closed = false;
        void Commit()
        {
            if (closed) return;
            closed = true;
            var v = inp.Text.Trim();
            if (v.Length > 0) g.Name = v;
            // 새로 만든 그룹은 곧바로 링크를 넣을 곳으로 삼는다
            if (isNew) SetTarget(g.Id);
            app.SaveLinks();
            Render();
            NameIn.Focus();
        }
        inp.LostKeyboardFocus += (_, _) => Commit();
        inp.PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter) { e.Handled = true; Commit(); }
            else if (e.Key == Key.Escape)
            {
                e.Handled = true;
                if (isNew) Commit(); else { closed = true; Render(); NameIn.Focus(); }
            }
        };
        inp.MouseLeftButtonUp += (_, e) => e.Handled = true;
    }

    // 지운 그룹의 링크는 '그룹 없음' 으로 옮긴다 - 링크가 사라지지 않으니 따로 묻지 않는다
    void DeleteGroup(LinkGroup g)
    {
        groups[0].Links.AddRange(g.Links);
        groups.Remove(g);
        if (settings.LinksTarget == g.Id) SetTarget(Inbox);
        app.SaveLinks();
        Render();
    }

    void MoveGroup(int i, int dir)
    {
        int j = i + dir;
        if (j < 1 || j >= groups.Count) return;
        (groups[i], groups[j]) = (groups[j], groups[i]);
        app.SaveLinks();
        Render();
    }

    void MoveLink(int fromG, int fromI, int toG, int toI)
    {
        if (fromI < 0) return;
        var lk = groups[fromG].Links[fromI];
        groups[fromG].Links.RemoveAt(fromI);
        if (fromG == toG && toI > fromI) toI--;
        groups[toG].Links.Insert(Math.Min(toI, groups[toG].Links.Count), lk);
        app.SaveLinks();
        Render();
        Flash(lk, scrollInto: false);
    }

    void EditLink(LinkItem lk)
    {
        var dlg = new EditLinkWindow(lk) { Owner = this };
        if (dlg.ShowDialog() == true)
        {
            lk.Name = dlg.ResultName;
            lk.Url = dlg.ResultUrl;
            app.SaveLinks();
            Render();
        }
    }

    /* ─── 드래그 ───
       놓을 자리: 행 위/아래(선), 그룹 머리나 빈 그룹(그룹 끝에 넣기) */
    sealed class DragState { public LinkRow Row = null!; public Point Start; public bool Moved; }
    DragState? drag;
    (int Gi, int Li)? dropAt;
    bool dragActive, justDragged;
    DispatcherTimer? autoScroll;
    bool hadCapture;
    double lastY;
    Section? revealed;

    void OnDragMove(object sender, MouseEventArgs e)
    {
        if (drag == null) return;
        if (e.LeftButton != MouseButtonState.Pressed) { EndDrag(false); return; }
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
            dragActive = true;
            drag.Row.Host.Opacity = .35;
            drag.Row.Card.Effect = null;
            Mouse.OverrideCursor = Cursors.SizeAll;
            hadCapture = ListScroll.CaptureMouse();
            RevealInbox();
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

    // 숨겨 둔 빈 '그룹 없음' 칸을 끌어 놓을 곳으로 드러낸다.
    // 위에 끼우면 목록 전체가 밀려 내려가므로 맨 아래에 붙인다.
    void RevealInbox()
    {
        if (!HasUserGroups || groups[0].Links.Count > 0 || sections.Any(x => x.Gi == 0)) return;
        revealed = BuildSection(groups[0], 0, new List<LinkItem>(), "", false);
        sections.Add(revealed);
        ListPanel.Children.Add(revealed.Panel);
    }

    void UpdateDrop(double y)
    {
        ClearDropMarks();
        dropAt = null;
        if (drag == null) return;
        var sec = sections.FirstOrDefault(x =>
        {
            double top = x.Panel.TranslatePoint(new Point(0, 0), ListPanel).Y;
            return y >= top && y < top + x.Panel.ActualHeight;
        });
        if (sec == null) return;
        int gi = sec.Gi;
        bool same = gi == drag.Row.Gi;
        int from = drag.Row.Li;

        void Into()
        {
            int to = groups[gi].Links.Count;
            if (same && (to == from || to == from + 1)) return;
            dropAt = (gi, to);
            MarkInto(sec, true);
        }

        double headBottom = sec.Head == null ? double.NegativeInfinity
            : sec.Head.TranslatePoint(new Point(0, sec.Head.ActualHeight), ListPanel).Y;
        var rows = sec.Collapsed ? new List<LinkRow>() : sec.Rows;
        if (rows.Count == 0 || y < headBottom) { Into(); return; }

        int i = rows.FindIndex(r =>
        {
            double top = r.Host.TranslatePoint(new Point(0, 0), ListPanel).Y;
            return y < top + r.Host.ActualHeight / 2;
        });
        if (i < 0) i = rows.Count;
        if (same && (i == from || i == from + 1)) return;
        dropAt = (gi, i);
        if (i < rows.Count) rows[i].Above.Visibility = Visibility.Visible;
        else rows[^1].Below.Visibility = Visibility.Visible;
    }

    void MarkInto(Section sec, bool on)
    {
        if (sec.Head != null)
        {
            sec.Head.Background = on ? B("AccentSoft") : Brushes.Transparent;
            sec.Head.BorderBrush = on ? B("Accent") : Brushes.Transparent;
        }
        if (sec.EmptyBox != null)
        {
            sec.EmptyBox.Background = on ? B("AccentSoft") : Brushes.Transparent;
            sec.EmptyDash!.Stroke = on ? B("Accent") : B("BorderStrong");
            sec.EmptyText!.Foreground = on ? B("Accent") : B("Text3");
        }
    }

    void ClearDropMarks()
    {
        foreach (var sec in sections)
        {
            MarkInto(sec, false);
            foreach (var r in sec.Rows) { r.Above.Visibility = Visibility.Collapsed; r.Below.Visibility = Visibility.Collapsed; }
        }
    }

    void OnDragUp(object sender, MouseButtonEventArgs e)
    {
        if (drag == null) return;
        if (!drag.Moved) { drag = null; return; }
        e.Handled = true;
        EndDrag(true);
    }

    void EndDrag(bool commit)
    {
        var d = drag;
        drag = null;
        dragActive = false;
        autoScroll?.Stop();
        autoScroll = null;
        Mouse.OverrideCursor = null;
        hadCapture = false;
        if (ListScroll.IsMouseCaptured) ListScroll.ReleaseMouseCapture();
        if (d == null || !d.Moved) return;
        justDragged = true;
        Dispatcher.BeginInvoke(() => justDragged = false, DispatcherPriority.Input);
        var at = dropAt;
        dropAt = null;
        revealed = null;
        if (commit && at is (int gi, int li)) MoveLink(d.Row.Gi, d.Row.Li, gi, li);
        else Render();
    }

    /* ─── 키 ─── */
    void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (Keyboard.Modifiers == ModifierKeys.Control)
        {
            switch (key)
            {
                case Key.F: e.Handled = true; Q.Focus(); Q.SelectAll(); return;
                case Key.T: e.Handled = true; app.SetAlwaysOnTop(!settings.AlwaysOnTop); return;
                case Key.Q: e.Handled = true; app.Quit(); return;
                case Key.L: e.Handled = true; return;
            }
        }
        if (key == Key.Escape && Keyboard.Modifiers == ModifierKeys.None && Q.Text != "")
        {
            e.Handled = true;
            Q.Text = "";
            Q.Focus();
        }
    }

    /* ─── 창 위치 ─── */
    void RestoreSavedBounds()
    {
        var b = settings.LinksBounds;
        if (b == null || b.Width < MinWidth || b.Height < MinHeight) { WindowStartupLocation = WindowStartupLocation.CenterScreen; return; }
        var vs = new Rect(SystemParameters.VirtualScreenLeft, SystemParameters.VirtualScreenTop,
                          SystemParameters.VirtualScreenWidth, SystemParameters.VirtualScreenHeight);
        Width = b.Width; Height = b.Height;
        if (!vs.IntersectsWith(new Rect(b.X + 40, b.Y + 10, Math.Max(1, b.Width - 80), 30)))
        {
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            return;
        }
        WindowStartupLocation = WindowStartupLocation.Manual;
        Left = b.X; Top = b.Y;
    }

    void OnClosing(object? sender, CancelEventArgs e)
    {
        var rb = WindowState == WindowState.Normal ? new Rect(Left, Top, ActualWidth, ActualHeight) : RestoreBounds;
        if (!rb.IsEmpty) settings.LinksBounds = new Bounds { X = rb.X, Y = rb.Y, Width = rb.Width, Height = rb.Height };
        app.SaveSettings();
    }
}
