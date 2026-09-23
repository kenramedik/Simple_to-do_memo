using System;
using System.Drawing;
using SimpleToDoMemo.Core;
using Forms = System.Windows.Forms;

namespace SimpleToDoMemo.Views;

// 최소화했을 때만 알림 영역에 뜨는 아이콘. 창을 되살리면 없앤다.
public sealed class Tray : IDisposable
{
    readonly App app;
    readonly MainWindow win;
    readonly Forms.NotifyIcon icon;

    public Tray(App app, MainWindow win)
    {
        this.app = app;
        this.win = win;
        var stream = System.Windows.Application.GetResourceStream(new Uri("pack://application:,,,/Assets/icon.ico")).Stream;
        icon = new Forms.NotifyIcon { Icon = new Icon(stream, Forms.SystemInformation.SmallIconSize), Visible = true };
        icon.MouseClick += (_, e) => { if (e.Button == Forms.MouseButtons.Left) win.ShowFromTray(); };
        icon.MouseDoubleClick += (_, _) => win.ShowFromTray();
        Refresh();
    }

    public void Refresh()
    {
        var T = L.Cur;
        icon.Text = $"{L.Plain(T.AppTitle)}  v{App.Version}";
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add(T.Open, null, (_, _) => win.ShowFromTray());
        var top = new Forms.ToolStripMenuItem(L.Plain(T.AlwaysOnTop)) { Checked = app.Settings.AlwaysOnTop };
        top.Click += (_, _) => app.SetAlwaysOnTop(!app.Settings.AlwaysOnTop);
        menu.Items.Add(top);
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(L.Plain(T.Exit), null, (_, _) => app.Quit());
        var old = icon.ContextMenuStrip;
        icon.ContextMenuStrip = menu;
        old?.Dispose();
    }

    public void Dispose()
    {
        icon.Visible = false;
        icon.ContextMenuStrip?.Dispose();
        icon.Dispose();
    }
}
