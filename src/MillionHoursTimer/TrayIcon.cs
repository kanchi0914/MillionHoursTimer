using System.Windows;
using Forms = System.Windows.Forms;

namespace MillionHoursTimer;

/// <summary>タスクトレイのアイコン。左クリックでウィンドウを開き、右クリックで終了できる。</summary>
internal sealed class TrayIcon : IDisposable
{
    private readonly Forms.NotifyIcon _icon;

    public TrayIcon(Action open, Action exit)
    {
        var stream = Application.GetResourceStream(new Uri("pack://application:,,,/Resources/app.ico"))!.Stream;
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("開く", null, (_, _) => open());
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("終了", null, (_, _) => exit());

        _icon = new Forms.NotifyIcon
        {
            Icon = new System.Drawing.Icon(stream),
            Text = "MillionHoursTimer",
            ContextMenuStrip = menu,
            Visible = true,
        };
        _icon.MouseClick += (_, e) =>
        {
            if (e.Button == Forms.MouseButtons.Left) open();
        };
    }

    /// <summary>ツールチップの文字列 (最大63文字)</summary>
    public string Text
    {
        set => _icon.Text = value.Length > 63 ? value[..63] : value;
    }

    public void ShowBalloon(string title, string text) => _icon.ShowBalloonTip(5000, title, text, Forms.ToolTipIcon.Info);

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
    }
}
