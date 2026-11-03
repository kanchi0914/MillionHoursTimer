using System.Windows;
using System.Windows.Controls;
using MillionHoursTimer.Core;

namespace MillionHoursTimer.UI;

/// <summary>ファイル別の記録の自動削除をアプリごとに設定する</summary>
public partial class FileCleanupDialog : Window
{
    private const int DefaultDays = 30;
    private const int DefaultKeepMinutes = 10;

    private readonly TrackerService _tracker;
    private readonly string _process;

    internal FileCleanupDialog(TrackerService tracker, string process, string appName)
    {
        _tracker = tracker;
        _process = process;
        InitializeComponent();

        var app = tracker.GetSnapshot().Apps.FirstOrDefault(a => string.Equals(a.Process, process, StringComparison.OrdinalIgnoreCase));
        var days = app?.FileExpireDays ?? 0;
        Message.Text = $"{appName} のファイル別の記録のうち、しばらく作業していない項目を自動で削除します。";
        EnabledBox.IsChecked = days > 0;
        DaysBox.Text = (days > 0 ? days : DefaultDays).ToString();
        // 初めて設定するときは、短い項目だけ消すようにしておく
        KeepBox.Text = (days > 0 ? app!.FileKeepMinutes : DefaultKeepMinutes).ToString();
        UpdateEnabled();
    }

    private void OnEnabledChanged(object sender, RoutedEventArgs e) => UpdateEnabled();

    private void UpdateEnabled()
    {
        if (DaysBox == null || KeepRow == null) return;
        DaysBox.IsEnabled = EnabledBox.IsChecked == true;
        KeepRow.IsEnabled = EnabledBox.IsChecked == true;
    }

    private void OnOk(object sender, RoutedEventArgs e)
    {
        var days = 0;
        var keep = 0;
        if (EnabledBox.IsChecked == true
            && (!TryReadNumber(DaysBox, "日数", 1, 36500, out days) || !TryReadNumber(KeepBox, "残す累積時間", 0, 1_000_000, out keep)))
        {
            return;
        }

        var count = _tracker.CountExpiredFiles(_process, days, keep);
        if (count > 0 && !Dialogs.Confirm(this, $"この設定にすると、今ある {count}件 の記録がすぐに削除されます。よろしいですか？\n(アプリ全体の作業時間は変わりません)"))
        {
            return;
        }

        try
        {
            _tracker.SetFileCleanup(_process, days, keep);
        }
        catch (Exception ex) when (Dialogs.IsSaveError(ex))
        {
            Dialogs.SaveFailed(this, ex);
            return;
        }
        DialogResult = true;
    }

    private bool TryReadNumber(TextBox box, string label, int min, int max, out int value)
    {
        if (int.TryParse(box.Text.Trim(), out value) && value >= min && value <= max) return true;
        Dialogs.Warn(this, $"{label}には {min}〜{max} の数字を入力してください。");
        box.Focus();
        box.SelectAll();
        return false;
    }
}
