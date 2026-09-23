using System.Windows;
using System.Windows.Documents;
using SimpleToDoMemo.Core;
using static SimpleToDoMemo.Views.Ui;

namespace SimpleToDoMemo.Views;

// 설정 > 링크 설정 - 괄호 안 숫자에 달 주소
public partial class LinkSettingsWindow : Window
{
    public NumberLink Result { get; private set; } = new();

    public LinkSettingsWindow(NumberLink current)
    {
        InitializeComponent();
        var T = L.Cur;
        Title = T.LinkTitle;
        Sub.Text = T.LinkSub;
        UrlLabel.Text = T.LinkUrl;
        ParamLabel.Text = T.LinkParam;
        ParamOptional.Text = T.LinkOptional;
        CancelBtn.Content = T.Cancel;
        SaveBtn.Content = T.Save;
        UrlBox.Text = current.Url;
        ParamBox.Text = current.Param;
        UrlBox.TextChanged += (_, _) => Check();
        ParamBox.TextChanged += (_, _) => Check();
        SaveBtn.Click += (_, _) =>
        {
            Result = new NumberLink { Url = UrlBox.Text.Trim(), Param = ParamBox.Text.Trim() };
            DialogResult = true;
        };
        Check();
        Loaded += (_, _) => { UrlBox.Focus(); UrlBox.SelectAll(); };
    }

    void Check()
    {
        var T = L.Cur;
        string url = UrlBox.Text.Trim(), param = ParamBox.Text.Trim();
        bool badUrl = url != "" && !LinkUtil.UrlOk.IsMatch(url);
        bool badParam = param != "" && !LinkUtil.ParamOk.IsMatch(param);
        SetIsBad(UrlBox, badUrl);
        SetIsBad(ParamBox, badParam);
        SaveBtn.IsEnabled = !badUrl && !badParam;
        Preview.Inlines.Clear();
        Preview.Foreground = badUrl || badParam ? B("Red") : B("Text2");
        if (badUrl || badParam) Preview.Text = badUrl ? T.LinkBadUrl : T.LinkBadParam;
        else if (url == "") Preview.Text = T.LinkOff;
        else
        {
            Preview.Inlines.Add(new Run($"{T.LinkPreview}: (12345) → "));
            Preview.Inlines.Add(new Run(LinkUtil.Build(url, param, "12345")) { FontWeight = FontWeights.SemiBold, Foreground = B("Text") });
        }
    }
}
