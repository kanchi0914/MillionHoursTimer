using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using MillionHoursTimer.Core;
using MillionHoursTimer.Core.Model;
using MillionHoursTimer.Core.Toggl;
using MillionHoursTimer.Platform;

namespace MillionHoursTimer.UI;

public sealed class TogglAppRow : ObservableObject
{
    private bool _enabled;
    private string _project = "";
    private string _tag = "";

    public required string Process { get; init; }
    public required string Name { get; init; }

    public bool Enabled
    {
        get => _enabled;
        set => Set(ref _enabled, value);
    }

    public string Project
    {
        get => _project;
        set => Set(ref _project, value ?? "");
    }

    public string Tag
    {
        get => _tag;
        set => Set(ref _tag, value ?? "");
    }
}

public partial class SettingsWindow : Window
{
    private readonly TrackerService _tracker;
    private readonly TogglService _toggl;
    private readonly ILog _log;
    private readonly List<TogglAppRow> _togglRows;

    internal SettingsWindow(TrackerService tracker, TogglService toggl, ILog log)
    {
        _tracker = tracker;
        _toggl = toggl;
        _log = log;
        InitializeComponent();

        var s = tracker.Settings;
        AutoStartBox.IsChecked = s.AutoStart;
        DataDirBox.Text = tracker.Paths.Root;
        ModeAll.IsChecked = s.CountMode == CountMode.All;
        ModeNotMinimized.IsChecked = s.CountMode == CountMode.NotMinimized;
        ModeActive.IsChecked = s.CountMode == CountMode.ActiveOnly;
        IntervalBox.Text = s.CountIntervalSeconds.ToString();
        IdleBox.Text = s.IdleTimeoutSeconds.ToString();
        MinRecordBox.Text = s.MinRecordSeconds.ToString();
        HyphenBox.IsChecked = s.UseHyphenFileName;
        SplitBox.IsChecked = s.SplitFileNameBySpace;
        IgnoreStateBox.IsChecked = s.IgnoreWindowStateForFiles;
        MinFileRecordBox.Text = s.MinFileRecordSeconds.ToString();
        TogglMinBox.Text = s.TogglMinSeconds.ToString();

        _togglRows = tracker.GetSnapshot().Apps.Select(a => new TogglAppRow
        {
            Process = a.Process,
            Name = a.Name,
            Enabled = a.TogglEnabled,
            Project = a.TogglProject,
            Tag = a.TogglTag,
        }).ToList();
        TogglApps.ItemsSource = _togglRows;
        RefreshToggl();
    }

    public ObservableCollection<string> ProjectNames { get; } = [];
    public ObservableCollection<string> TagNames { get; } = [];

    private void RefreshToggl()
    {
        ProjectNames.Clear();
        foreach (var p in _toggl.ProjectNames) ProjectNames.Add(p);
        TagNames.Clear();
        foreach (var t in _toggl.TagNames) TagNames.Add(t);

        if (!_toggl.HasApiKey)
        {
            TogglStatus.Text = "Toggl とは連携していません。";
        }
        else if (_toggl.IsConnected)
        {
            TogglStatus.Text = $"連携中: ワークスペース {_toggl.Workspace!.WorkspaceId}" + (_toggl.PendingCount > 0 ? $"　(未送信 {_toggl.PendingCount}件)" : "");
        }
        else
        {
            TogglStatus.Text = "API キーは保存されていますが、Toggl に接続できていません。" +
                (_toggl.LastError.Length > 0 ? "\n" + _toggl.LastError : "");
        }
        DisconnectButton.IsEnabled = _toggl.HasApiKey;
    }

    private async void OnConnect(object sender, RoutedEventArgs e)
    {
        var key = ApiKeyBox.Password.Trim();
        var url = WorkspaceUrlBox.Text.Trim();
        if (key.Length == 0 && !_toggl.HasApiKey)
        {
            Dialogs.Info(this, "API キーを入力してください。");
            return;
        }
        if (url.Length == 0 && !_toggl.HasApiKey)
        {
            Dialogs.Info(this, "Toggl にログインしたときの URL を入力してください。");
            return;
        }
        var workspace = url.Length > 0 ? TogglWorkspace.FromUrl(url) : null;
        if (url.Length > 0 && workspace == null)
        {
            Dialogs.Warn(this, "URL の形式が違います。Toggl にログインしたときの、https://focus.toggl.com/数字/workspaces/数字/... の形の URL を貼り付けてください。");
            return;
        }

        ConnectButton.IsEnabled = false;
        TogglStatus.Text = "認証しています...";
        try
        {
            await _toggl.ConnectAsync(key.Length > 0 ? key : null, workspace);
            ApiKeyBox.Clear();
            WorkspaceUrlBox.Clear();
            RefreshToggl();
            if (_toggl.IsConnected)
            {
                Dialogs.Info(this, "認証が完了しました。");
                _ = _toggl.FlushAsync();
            }
        }
        catch (TogglApiException ex)
        {
            RefreshToggl();
            var hint = !ex.IsAuthError ? ""
                : key.Length > 0 && !key.StartsWith("toggl_sk_", StringComparison.Ordinal)
                    ? "認証に失敗しました。Toggl 2.0 の API キー (toggl_sk_ で始まるもの) を貼り付けてください。旧 Toggl Track の API Token は使えません。\n\n"
                    : "認証に失敗しました。API キーと URL が正しいか確認してください。\n\n";
            Dialogs.Warn(this, hint + ex.Message);
        }
        finally
        {
            ConnectButton.IsEnabled = true;
        }
    }

    private void OnDisconnect(object sender, RoutedEventArgs e)
    {
        if (!Dialogs.Confirm(this, "Toggl との連携を解除し、保存している API キーを削除します。よろしいですか？")) return;
        _toggl.ClearApiKey();
        RefreshToggl();
    }

    private void OnOpenTogglSettings(object sender, RoutedEventArgs e) => Shell.Open("https://focus.toggl.com/settings");

    private void OnOpenDataFolder(object sender, RoutedEventArgs e) => Shell.Open(_tracker.Paths.Root);

    private void OnOk(object sender, RoutedEventArgs e)
    {
        var s = _tracker.Settings;
        if (!TryReadNumber(IntervalBox, "計測間隔", 1, 3600, out var interval)
            || !TryReadNumber(IdleBox, "無操作判定時間", 0, 86400, out var idle)
            || !TryReadNumber(MinRecordBox, "最小記録時間", 0, 86400, out var minRecord)
            || !TryReadNumber(MinFileRecordBox, "ファイル別の最小記録時間", 0, 86400, out var minFileRecord)
            || !TryReadNumber(TogglMinBox, "最低送信時間", 0, 86400, out var togglMin))
        {
            return;
        }

        s.AutoStart = AutoStartBox.IsChecked == true;
        s.CountMode = ModeActive.IsChecked == true ? CountMode.ActiveOnly
            : ModeAll.IsChecked == true ? CountMode.All
            : CountMode.NotMinimized;
        s.CountIntervalSeconds = interval;
        s.IdleTimeoutSeconds = idle;
        s.MinRecordSeconds = minRecord;
        s.UseHyphenFileName = HyphenBox.IsChecked == true;
        s.SplitFileNameBySpace = SplitBox.IsChecked == true;
        s.IgnoreWindowStateForFiles = IgnoreStateBox.IsChecked == true;
        s.MinFileRecordSeconds = minFileRecord;
        s.TogglMinSeconds = togglMin;

        try
        {
            _tracker.UpdateSettings(s);
            var current = _tracker.GetSnapshot().Apps.ToDictionary(a => a.Process);
            foreach (var row in _togglRows)
            {
                if (!current.TryGetValue(row.Process, out var app)) continue;
                if (app.TogglEnabled == row.Enabled && app.TogglProject == row.Project.Trim() && app.TogglTag == row.Tag.Trim()) continue;
                _tracker.UpdateApp(row.Process, a =>
                {
                    a.TogglEnabled = row.Enabled;
                    a.TogglProject = row.Project.Trim();
                    a.TogglTag = row.Tag.Trim();
                });
            }
        }
        catch (Exception ex) when (Dialogs.IsSaveError(ex))
        {
            Dialogs.SaveFailed(this, ex);
            return;
        }

        try
        {
            AutoStart.Set(s.AutoStart);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException or IOException)
        {
            _log.Error("自動起動を設定できませんでした", ex);
            Dialogs.Warn(this, "自動起動を設定できませんでした。\n" + ex.Message);
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
