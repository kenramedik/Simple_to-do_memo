using System.Windows;
using SimpleToDoMemo.Core;
using static SimpleToDoMemo.Views.Ui;

namespace SimpleToDoMemo.Views;

// 링크 모음 > 우클릭 > 수정
public partial class EditLinkWindow : Window
{
    public string ResultName { get; private set; } = "";
    public string ResultUrl { get; private set; } = "";

    public EditLinkWindow(LinkItem link)
    {
        InitializeComponent();
        var T = L.Cur;
        Title = T.EditLinkTitle;
        NameLabel.Text = T.Name;
        UrlLabel.Text = T.Url;
        CancelBtn.Content = T.Cancel;
        SaveBtn.Content = T.Save;
        Hint.Text = T.BadUrl;
        NameBox.Text = link.Name;
        UrlBox.Text = link.Url;
        UrlBox.TextChanged += (_, _) => Check();
        SaveBtn.Click += (_, _) =>
        {
            var url = LinkUtil.Normalize(UrlBox.Text);
            if (url == null) return;
            ResultUrl = url;
            ResultName = NameBox.Text.Trim() is { Length: > 0 } n ? n : LinkUtil.DefaultName(url);
            DialogResult = true;
        };
        Check();
        Loaded += (_, _) => { NameBox.Focus(); NameBox.SelectAll(); };
    }

    void Check()
    {
        bool bad = LinkUtil.Normalize(UrlBox.Text) == null;
        SetIsBad(UrlBox, bad);
        Hint.Visibility = bad ? Visibility.Visible : Visibility.Collapsed;
        SaveBtn.IsEnabled = !bad;
    }
}
