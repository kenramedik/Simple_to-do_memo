using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Shapes;

namespace SimpleToDoMemo.Views;

// 화면 조립에 쓰는 공용 도구 - 아이콘, 툴팁, 색, 첨부 속성
public static class Ui
{
    /* ─── 첨부 속성 ─── */
    public static readonly DependencyProperty RadiusProperty = DependencyProperty.RegisterAttached(
        "Radius", typeof(CornerRadius), typeof(Ui), new FrameworkPropertyMetadata(new CornerRadius(0)));
    public static CornerRadius GetRadius(DependencyObject o) => (CornerRadius)o.GetValue(RadiusProperty);
    public static void SetRadius(DependencyObject o, CornerRadius v) => o.SetValue(RadiusProperty, v);

    public static readonly DependencyProperty IsBadProperty = DependencyProperty.RegisterAttached(
        "IsBad", typeof(bool), typeof(Ui), new FrameworkPropertyMetadata(false));
    public static bool GetIsBad(DependencyObject o) => (bool)o.GetValue(IsBadProperty);
    public static void SetIsBad(DependencyObject o, bool v) => o.SetValue(IsBadProperty, v);

    /* ─── 리소스 ─── */
    public static Brush B(string key) => (Brush)Application.Current.Resources[key];
    public static Style S(string key) => (Style)Application.Current.Resources[key];
    public static object R(string key) => Application.Current.Resources[key];

    public static SolidColorBrush Hex(string hex) => new((Color)ColorConverter.ConvertFromString(hex));

    // 항목 색상 - Electron 판과 같은 값
    public static readonly Dictionary<string, string> Colors = new()
    {
        ["red"] = "#C81E1E", ["orange"] = "#D9600A", ["amber"] = "#CA8A04", ["green"] = "#15803D",
        ["teal"] = "#006B8A", ["blue"] = "#1D4ED8", ["purple"] = "#7E22CE", ["gray"] = "#57574E",
    };
    // 노랑은 흰 체크와 대비가 2.9:1 밖에 안 나온다 - 이 색만 체크를 어둡게(5.8:1)
    public static readonly Dictionary<string, string> Ink = new() { ["amber"] = "#1C1C1A" };

    public static Color? ColorOf(string? key) =>
        key != null && Colors.TryGetValue(key, out var hex) ? (Color)ColorConverter.ConvertFromString(hex) : null;

    // CSS 의 color-mix(in srgb, c p%, white)
    public static SolidColorBrush Mix(Color c, double p)
    {
        byte M(byte v) => (byte)System.Math.Round(v * p + 255 * (1 - p));
        return new SolidColorBrush(Color.FromRgb(M(c.R), M(c.G), M(c.B)));
    }

    /* ─── 아이콘 ─── 24x24 좌표의 SVG 경로를 그대로 쓴다 */
    public static class Ico
    {
        public const string Check = "M4 12.5l5.5 5.5L20 6.5";
        public const string Pen = "M12 20h9 M16.5 3.5a2.1 2.1 0 0 1 3 3L7 19l-4 1 1-4z";
        public const string Pin = "M12 17v5 M9 10.8a2 2 0 0 1-1.1 1.8l-1.8 0.9A2 2 0 0 0 5 15.2V16a1 1 0 0 0 1 1h12a1 1 0 0 0 1-1v-0.8a2 2 0 0 0-1.1-1.7l-1.8-0.9a2 2 0 0 1-1.1-1.8V7a1 1 0 0 1 1-1 2 2 0 0 0 0-4H8a2 2 0 0 0 0 4 1 1 0 0 1 1 1z";
        public const string Jump = "M7 17L17 7 M8 7h9v9";
        public const string Cross = "M6 6l12 12 M18 6L6 18";
        public const string Carry = "M3 12a9 9 0 0 1 15-6.7L21 8 M21 3v5h-5";
        public const string Trash = "M3 6h18 M8 6V4h8v2 M19 6l-1 14H6L5 6";
        public const string SortAsc = "M3 16l4 4 4-4 M7 20V4 M13 5h3 M13 11h6 M13 17h9";
        public const string SortDesc = "M3 16l4 4 4-4 M7 20V4 M13 5h9 M13 11h6 M13 17h3";
        public const string Search = "M4 11a7 7 0 1 0 14 0a7 7 0 1 0 -14 0 M20 20l-4.3-4.3";
        public const string ChevL = "M15 18l-6-6 6-6";
        public const string ChevR = "M9 18l6-6-6-6";
        public const string ChevD = "M6 9l6 6 6-6";
        public const string Arrow = "M5 12h13 M12 5l7 7-7 7";
        public const string Plus = "M12 5v14 M5 12h14";
        public const string Link = "M10 13a5 5 0 0 0 7.5 0.5l3-3a5 5 0 0 0-7-7l-1.7 1.7 M14 11a5 5 0 0 0-7.5 -0.5l-3 3a5 5 0 0 0 7 7l1.7-1.7";
        public const string Calendar = "M6 4.5h12a3 3 0 0 1 3 3v10a3 3 0 0 1-3 3H6a3 3 0 0 1-3-3v-10a3 3 0 0 1 3-3z M8 2.5v4 M16 2.5v4 M3.5 10h17";
    }

    // 선 아이콘. 색은 부모의 글자색(TextElement.Foreground)을 따라간다.
    public static FrameworkElement Icon(string data, double size, double stroke, bool fill = false)
    {
        var path = new Path
        {
            Data = Geometry.Parse(data),
            StrokeThickness = stroke,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            StrokeLineJoin = PenLineJoin.Round,
        };
        var fg = new Binding("(TextElement.Foreground)") { RelativeSource = RelativeSource.Self };
        path.SetBinding(Shape.StrokeProperty, fg);
        if (fill) path.SetBinding(Shape.FillProperty, fg);
        var canvas = new Canvas { Width = 24, Height = 24 };
        canvas.Children.Add(path);
        return new Viewbox { Width = size, Height = size, Child = canvas, IsHitTestVisible = false };
    }

    // 끌어 옮기는 손잡이 - 점 여섯 개
    public static FrameworkElement Grip()
    {
        var c = new Canvas { Width = 11, Height = 15, IsHitTestVisible = false };
        foreach (var (x, y) in new[] { (2.2, 2.2), (8.8, 2.2), (2.2, 7.5), (8.8, 7.5), (2.2, 12.8), (8.8, 12.8) })
        {
            var e = new Ellipse { Width = 2.8, Height = 2.8 };
            e.SetBinding(Shape.FillProperty, new Binding("(TextElement.Foreground)") { RelativeSource = RelativeSource.Self });
            Canvas.SetLeft(e, x - 1.4);
            Canvas.SetTop(e, y - 1.4);
            c.Children.Add(e);
        }
        return c;
    }

    public static Button IconButton(string style, string icon, double size = 15, double stroke = 2.2)
    {
        var b = new Button { Style = S(style), Content = Icon(icon, size, stroke) };
        return b;
    }

    /* ─── 툴팁 ─── 운영체제 기본보다 빨리, 요소 위쪽에 띄운다 */
    public static void Tip(FrameworkElement el, string? text)
    {
        if (string.IsNullOrEmpty(text)) { el.ClearValue(FrameworkElement.ToolTipProperty); return; }
        el.ToolTip = text;
        ToolTipService.SetInitialShowDelay(el, 250);
        ToolTipService.SetBetweenShowDelay(el, 0);
        ToolTipService.SetPlacement(el, PlacementMode.Top);
        ToolTipService.SetShowsToolTipOnKeyboardFocus(el, false);
    }

    public static TextBlock Text(string text, double size, FontWeight weight, Brush fg) =>
        new() { Text = text, FontSize = size, FontWeight = weight, Foreground = fg, VerticalAlignment = VerticalAlignment.Center };

    // 비어 있을 때 보여주는 안내 (아이콘 + 두 줄)
    public static FrameworkElement Empty(string icon, string title, string sub)
    {
        var sp = new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 30),
        };
        TextElement.SetForeground(sp, B("Text3"));
        if (icon != "") sp.Children.Add(new ContentControl { Content = Icon(icon, 34, 1.3), Margin = new Thickness(0, 0, 0, 10), HorizontalAlignment = HorizontalAlignment.Center, Focusable = false });
        sp.Children.Add(new TextBlock { Text = title, FontSize = 13.5, FontWeight = FontWeights.SemiBold, Foreground = B("Text2"), HorizontalAlignment = HorizontalAlignment.Center });
        sp.Children.Add(new TextBlock { Text = sub, FontSize = 12, Foreground = B("Text3"), HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 2, 0, 0) });
        return sp;
    }

    public static MenuItem Item(string header, System.Action run, string? tag = null, bool enabled = true)
    {
        var mi = new MenuItem { Header = header, Tag = tag, IsEnabled = enabled };
        mi.Click += (_, _) => run();
        return mi;
    }

    public static MenuItem Heading(string header) => new() { Header = header, Tag = "heading" };
}
