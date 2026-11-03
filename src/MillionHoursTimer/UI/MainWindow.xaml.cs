using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;
using MillionHoursTimer.Core;
using MillionHoursTimer.Core.Model;
using MillionHoursTimer.Core.Toggl;
using MillionHoursTimer.Platform;

namespace MillionHoursTimer.UI;

public sealed class AppRowViewModel(string process, ImageSource icon) : ObservableObject
{
    private string _name = "";
    private long _todaySeconds;
    private long _totalSeconds;
    private bool _isRecording;

    public string Process { get; } = process;
    public ImageSource Icon { get; } = icon;

    public string Name
    {
        get => _name;
        private set => Set(ref _name, value);
    }

    public long TodaySeconds
    {
        get => _todaySeconds;
        private set
        {
            if (Set(ref _todaySeconds, value)) Raise(nameof(TodayText));
        }
    }

    public long TotalSeconds
    {
        get => _totalSeconds;
        private set
        {
            if (Set(ref _totalSeconds, value)) Raise(nameof(TotalText));
        }
    }

    public bool IsRecording
    {
        get => _isRecording;
        private set => Set(ref _isRecording, value);
    }

    public string TodayText => TimeText.FormatDuration(TodaySeconds);
    public string TotalText => TimeText.FormatDuration(TotalSeconds);

    public void Update(AppStatus s)
    {
        Name = s.Name;
        TodaySeconds = s.TodaySeconds;
        TotalSeconds = s.TotalSeconds;
        IsRecording = s.IsRecording;
    }
}

public partial class MainWindow : Window
{
    private readonly TrackerService _tracker;
    private readonly TogglService _toggl;
    private readonly Updater _updater;
    private readonly ILog _log;
    private readonly Action _requestExit;
    private readonly Action _prepareRestart;
    private readonly ObservableCollection<AppRowViewModel> _rows = [];
    private readonly Dictionary<string, FileListWindow> _fileWindows = new(StringComparer.OrdinalIgnoreCase);
    private int _refreshQueued;

    internal MainWindow(TrackerService tracker, TogglService toggl, Updater updater, ILog log, Action requestExit, Action prepareRestart)
    {
        _tracker = tracker;
        _toggl = toggl;
        _updater = updater;
        _log = log;
        _requestExit = requestExit;
        _prepareRestart = prepareRestart;
        InitializeComponent();

        AppList.ItemsSource = _rows;
        _ = new ListSorter(AppList, new Dictionary<string, string>
        {
            ["アプリケーション名"] = nameof(AppRowViewModel.Name),
            ["今日の起動時間"] = nameof(AppRowViewModel.TodaySeconds),
            ["累積起動時間"] = nameof(AppRowViewModel.TotalSeconds),
        });

        // 計測は別スレッドで動いているので、画面の更新は溜めずに最新の状態だけ反映する
        _tracker.Updated += _ => QueueRefresh();
        _toggl.StateChanged += QueueRefresh;
        Refresh();
    }

    public bool AllowClose { get; set; }

    private void QueueRefresh()
    {
        if (Interlocked.Exchange(ref _refreshQueued, 1) == 1) return;
        Dispatcher.BeginInvoke(() =>
        {
            _refreshQueued = 0;
            Refresh();
        });
    }

    private void Refresh()
    {
        var snapshot = _tracker.GetSnapshot();
        var existing = _rows.ToDictionary(r => r.Process, StringComparer.OrdinalIgnoreCase);
        var current = snapshot.Apps.Select(a => a.Process).ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var row in _rows.Where(r => !current.Contains(r.Process)).ToList()) _rows.Remove(row);
        foreach (var status in snapshot.Apps)
        {
            if (!existing.TryGetValue(status.Process, out var row))
            {
                row = new AppRowViewModel(status.Process, LoadIcon(status));
                _rows.Add(row);
            }
            row.Update(status);
        }

        EmptyHint.Visibility = _rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        StatusText.Text = BuildStatus(snapshot);
        foreach (var window in _fileWindows.Values) window.Refresh();
    }

    private string BuildStatus(TrackerSnapshot snapshot)
    {
        var parts = new List<string>();
        if (snapshot.UnsavedCount > 0)
        {
            parts.Add($"⚠ {snapshot.UnsavedCount}件の記録をまだ保存できていません (記録ファイルを Excel などで開いていたら閉じてください)");
        }

        if (snapshot.IsIdle)
        {
            parts.Add("操作がないため計測を止めています");
        }
        else
        {
            var recording = snapshot.Apps.Where(a => a.IsRecording).Select(a => a.Name).ToList();
            parts.Add(recording.Count > 0 ? "記録中: " + string.Join("、", recording) : "記録中のアプリはありません");
        }

        if (_toggl.HasApiKey)
        {
            if (_toggl.PendingCount > 0) parts.Add($"Toggl 未送信 {_toggl.PendingCount}件");
            if (_toggl.LastError.Length > 0) parts.Add("Toggl: 送信エラー");
        }
        return string.Join("　|　", parts);
    }

    private ImageSource LoadIcon(AppStatus status)
    {
        var path = _tracker.Paths.IconFile(status.Process);
        if (!File.Exists(path) && File.Exists(status.ExePath)) IconHelper.TrySaveExeIcon(status.ExePath, path);
        return IconHelper.Load(path);
    }

    public void ShowAndActivate()
    {
        Show();
        if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
        Activate();
    }

    public void NotifyUpdate(string version)
    {
        UpdateNoticeText.Text = $"新しいバージョン {version} があります";
        UpdateNotice.Visibility = Visibility.Visible;
    }

    public void ShowMigration(bool firstRun)
    {
        ShowAndActivate();
        new MigrationWindow(_tracker, _toggl, firstRun) { Owner = this }.ShowDialog();
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        // ×ボタンでは終了せず、タスクトレイに常駐する
        if (AllowClose) return;
        e.Cancel = true;
        Hide();
    }

    #region アプリの登録

    private void OnAddApp(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "記録するアプリの実行ファイル (.exe) を選んでください",
            Filter = "実行ファイル (*.exe)|*.exe|ショートカット (*.lnk)|*.lnk",
        };
        if (dialog.ShowDialog(this) == true) AddFromPath(dialog.FileName);
    }

    private void OnDragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnDrop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is string[] files)
        {
            foreach (var file in files) AddFromPath(file);
        }
    }

    private void AddFromPath(string path)
    {
        try
        {
            if (Path.GetExtension(path).Equals(".lnk", StringComparison.OrdinalIgnoreCase))
            {
                path = Shortcut.ResolveTarget(path) ?? path;
            }
            if (!Path.GetExtension(path).Equals(".exe", StringComparison.OrdinalIgnoreCase))
            {
                Dialogs.Warn(this, $"実行ファイル (.exe) かそのショートカットを指定してください。\n{path}");
                return;
            }

            var process = Path.GetFileNameWithoutExtension(path);
            if (_tracker.HasApp(process))
            {
                Dialogs.Info(this, $"{process} は既に登録されています。");
                return;
            }

            var info = FileVersionInfo.GetVersionInfo(path);
            var name = new[] { info.FileDescription, info.ProductName, process }
                .First(n => !string.IsNullOrWhiteSpace(n))!.Trim();
            IconHelper.TrySaveExeIcon(path, _tracker.Paths.IconFile(process));
            _tracker.AddApp(process, name, path);
        }
        catch (Exception ex) when (Dialogs.IsSaveError(ex))
        {
            Dialogs.SaveFailed(this, ex);
        }
    }

    #endregion

    #region 右クリックメニュー

    private List<AppRowViewModel> SelectedRows => AppList.SelectedItems.Cast<AppRowViewModel>().ToList();

    private AppRowViewModel? SelectedRow => AppList.SelectedItem as AppRowViewModel;

    private void UpdateApp(string process, Action<AppDefinition> update)
    {
        try
        {
            _tracker.UpdateApp(process, update);
        }
        catch (Exception ex) when (Dialogs.IsSaveError(ex))
        {
            Dialogs.SaveFailed(this, ex);
        }
    }

    private void OnRename(object sender, RoutedEventArgs e)
    {
        if (SelectedRow is not { } row) return;
        var name = InputDialog.Ask(this, "表示名を変更", $"{row.Process} の表示名", row.Name);
        if (!string.IsNullOrEmpty(name)) UpdateApp(row.Process, a => a.Name = name);
    }

    private void OnSetExtensions(object sender, RoutedEventArgs e)
    {
        if (SelectedRow is not { } row) return;
        var current = _tracker.GetSnapshot().Apps.FirstOrDefault(a => a.Process == row.Process)?.Extensions ?? [];
        var text = InputDialog.Ask(this, "ファイル拡張子を設定",
            $"{row.Name} でファイル別に記録する拡張子を、半角スラッシュ区切りで入力してください。",
            AppDefinition.FormatExtensions(current),
            "例 (イラスト系ソフト): .gif/.jpg/.jpeg/.png/.bmp/.ico/.tif/.tiff/.tga/.psd/.psb/.sai\n" +
            "タイトルバーにファイル名が表示されるアプリで使えます。");
        if (text != null) UpdateApp(row.Process, a => a.Extensions = AppDefinition.ParseExtensions(text));
    }

    private void OnShowFiles(object sender, RoutedEventArgs e)
    {
        if (SelectedRow is { } row) ShowFiles(row);
    }

    private void OnRowDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (SelectedRow is { } row) ShowFiles(row);
    }

    private void ShowFiles(AppRowViewModel row)
    {
        if (!_fileWindows.TryGetValue(row.Process, out var window))
        {
            window = new FileListWindow(_tracker, row.Process, row.Name) { Owner = this };
            window.Closed += (_, _) => _fileWindows.Remove(row.Process);
            _fileWindows[row.Process] = window;
        }
        window.Show();
        window.Activate();
    }

    private void OnCopy(object sender, RoutedEventArgs e)
    {
        var text = new StringBuilder();
        foreach (var row in SelectedRows)
        {
            text.AppendLine($"{row.Name}の起動時間 今日:{row.TodayText.Trim()} 累積:{row.TotalText.Trim()}");
        }
        if (text.Length > 0) Clipboard.SetText(text.ToString());
    }

    private void OnRemove(object sender, RoutedEventArgs e)
    {
        var rows = SelectedRows;
        if (rows.Count == 0) return;

        var names = string.Join("、", rows.Select(r => r.Name));
        var answer = MessageBox.Show(this,
            $"「{names}」を一覧から削除します。\n\n" +
            "［はい］記録も削除する\n" +
            "［いいえ］一覧から外すだけにする (同じアプリを登録し直すと記録が戻ります)\n" +
            "［キャンセル］やめる",
            "一覧から削除", MessageBoxButton.YesNoCancel, MessageBoxImage.Question, MessageBoxResult.Cancel);
        if (answer == MessageBoxResult.Cancel) return;

        try
        {
            foreach (var row in rows)
            {
                if (_fileWindows.TryGetValue(row.Process, out var window)) window.Close();
                _tracker.RemoveApp(row.Process, deleteRecords: answer == MessageBoxResult.Yes);
            }
        }
        catch (Exception ex) when (Dialogs.IsSaveError(ex))
        {
            Dialogs.SaveFailed(this, ex);
        }
    }

    #endregion

    #region メニュー

    private void OnOpenDataFolder(object sender, RoutedEventArgs e) => Shell.Open(_tracker.Paths.Root);

    private void OnBackup(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = "バックアップ先のフォルダを選んでください" };
        if (dialog.ShowDialog(this) != true) return;

        var dest = Path.Combine(dialog.FolderName, $"MillionHoursTimer-backup-{DateTime.Now:yyyyMMdd-HHmmss}");
        try
        {
            CopyDirectory(_tracker.Paths.Root, dest);
            Dialogs.Info(this, $"データをバックアップしました。\n{dest}");
            Shell.Open(dest);
        }
        catch (Exception ex) when (Dialogs.IsSaveError(ex))
        {
            _log.Error("バックアップに失敗しました", ex);
            Dialogs.Warn(this, "バックアップに失敗しました。\n" + ex.Message);
        }
    }

    private static void CopyDirectory(string source, string dest)
    {
        Directory.CreateDirectory(dest);
        foreach (var file in Directory.GetFiles(source))
        {
            if (file.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase)) continue;
            File.Copy(file, Path.Combine(dest, Path.GetFileName(file)));
        }
        foreach (var dir in Directory.GetDirectories(source)) CopyDirectory(dir, Path.Combine(dest, Path.GetFileName(dir)));
    }

    private void OnImportLegacy(object sender, RoutedEventArgs e) => ShowMigration(firstRun: false);

    private void OnHistory(object sender, RoutedEventArgs e) => new HistoryWindow(_tracker) { Owner = this }.Show();

    private void OnSettings(object sender, RoutedEventArgs e) => new SettingsWindow(_tracker, _toggl, _log) { Owner = this }.ShowDialog();

    private void OnExit(object sender, RoutedEventArgs e) => _requestExit();

    private void OnHelp(object sender, RoutedEventArgs e) => Shell.Open(Updater.RepositoryUrl + "#readme");

    private void OnAbout(object sender, RoutedEventArgs e) =>
        Dialogs.Info(this, $"MillionHoursTimer {App.Version}\n\nデータの保存先:\n{_tracker.Paths.Root}\n\n{Updater.RepositoryUrl}");

    private async void OnCheckUpdate(object sender, RoutedEventArgs e)
    {
        if (!_updater.IsAvailable)
        {
            Dialogs.Info(this, "更新の確認は、インストーラー版・ポータブル版で使えます。");
            return;
        }
        var version = await _updater.CheckAsync();
        if (version == null)
        {
            Dialogs.Info(this, $"お使いのバージョン ({App.Version}) が最新です。");
            return;
        }
        NotifyUpdate(version);
        await InstallUpdateAsync(version);
    }

    private async void OnInstallUpdate(object sender, RoutedEventArgs e)
    {
        if (_updater.AvailableVersion is { } version) await InstallUpdateAsync(version);
    }

    private async Task InstallUpdateAsync(string version)
    {
        if (!Dialogs.Confirm(this, $"バージョン {version} に更新します。更新が終わると自動で再起動します。\n(記録中の作業は保存されます)")) return;
        try
        {
            UpdateNoticeText.Text = "更新をダウンロードしています...";
            await _updater.DownloadAsync(p => Dispatcher.BeginInvoke(() => UpdateNoticeText.Text = $"更新をダウンロードしています... {p}%"));
            _prepareRestart();
            _updater.ApplyAndRestart();
        }
        catch (Exception ex)
        {
            _log.Error("更新に失敗しました", ex);
            UpdateNoticeText.Text = $"新しいバージョン {version} があります";
            Dialogs.Warn(this, "更新に失敗しました。時間をおいてもう一度お試しください。\n" + ex.Message);
        }
    }

    #endregion
}
