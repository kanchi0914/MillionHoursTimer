using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using MillionHoursTimer.Core;
using MillionHoursTimer.Core.Legacy;
using MillionHoursTimer.Core.Toggl;
using MillionHoursTimer.Platform;

namespace MillionHoursTimer.UI;

public partial class MigrationWindow : Window
{
    private readonly TrackerService _tracker;
    private readonly TogglService _toggl;
    private readonly string? _userConfig = LegacyLocator.FindUserConfig();
    private LegacyData? _selected;

    internal MigrationWindow(TrackerService tracker, TogglService toggl, bool firstRun)
    {
        _tracker = tracker;
        _toggl = toggl;
        InitializeComponent();

        Intro.Text = firstRun
            ? "MillionHoursTimer v2 へようこそ。旧バージョン (MHTimer v1) を使っていた場合は、これまでの記録と設定を取り込めます。"
            : "旧バージョン (MHTimer v1) の記録と設定を取り込みます。";
        if (tracker.Settings.Migration is { } done)
        {
            Intro.Text += $"\n\n※ {done.MigratedAt:yyyy/MM/dd HH:mm} に取り込み済みです ({done.SourceDir})。もう一度取り込むと累計時間が二重になります。";
        }
        SkipButton.Visibility = firstRun ? Visibility.Visible : Visibility.Collapsed;
        AutoStartBox.Visibility = LegacyLocator.OldRunCommand() != null ? Visibility.Visible : Visibility.Collapsed;
        if (_userConfig == null)
        {
            SettingsBox.IsEnabled = false;
            SettingsBox.IsChecked = false;
            SettingsBox.Content = "設定と Toggl の API キーも引き継ぐ (旧バージョンの設定が見つかりませんでした)";
        }

        UpdateRunningPanel();
        foreach (var dir in LegacyLocator.FindDataDirs()) Candidates.Items.Add(dir);
        NotFound.Visibility = Candidates.Items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        if (Candidates.Items.Count > 0) Candidates.SelectedIndex = 0;
    }

    private void UpdateRunningPanel() =>
        RunningPanel.Visibility = LegacyLocator.RunningOldInstances().Count > 0 ? Visibility.Visible : Visibility.Collapsed;

    private void OnStopOld(object sender, RoutedEventArgs e)
    {
        App.StopOldInstances(this);
        UpdateRunningPanel();
        if (Candidates.SelectedItem is string dir) Select(dir);
    }

    private void OnBrowse(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = "旧バージョンの MHTimer.exe があるフォルダ、または data フォルダを選んでください" };
        if (dialog.ShowDialog(this) != true) return;

        var dir = LegacyImporter.ResolveDataDir(dialog.FolderName);
        if (dir == null)
        {
            Dialogs.Warn(this, "選んだフォルダに旧バージョンのデータ (data\\appData.csv) が見つかりませんでした。");
            return;
        }
        if (!Candidates.Items.Contains(dir)) Candidates.Items.Insert(0, dir);
        Candidates.SelectedItem = dir;
        NotFound.Visibility = Visibility.Collapsed;
    }

    private void OnCandidateChanged(object sender, SelectionChangedEventArgs e)
    {
        if (Candidates.SelectedItem is string dir) Select(dir);
    }

    private void Select(string dir)
    {
        try
        {
            _selected = LegacyImporter.Read(dir);
            var lines = new List<string>
            {
                $"アプリ {_selected.Apps.Count} 件 / 累計 {TimeText.FormatShort(_selected.TotalSeconds)} / ファイル別の記録 {_selected.FileCount} 件",
                $"最終更新: {_selected.LastWriteTime:yyyy/MM/dd HH:mm}",
            };
            lines.AddRange(_selected.Apps.Take(6).Select(a => $"・{a.Process}　{TimeText.FormatShort(a.TotalSeconds)}"));
            if (_selected.Apps.Count > 6) lines.Add($"　ほか {_selected.Apps.Count - 6} 件");
            lines.AddRange(_selected.Warnings.Select(w => "⚠ " + w));
            Preview.Text = string.Join("\n", lines);
            ImportButton.IsEnabled = true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _selected = null;
            Preview.Text = "読み込めませんでした: " + ex.Message;
            ImportButton.IsEnabled = false;
        }
    }

    private async void OnImport(object sender, RoutedEventArgs e)
    {
        if (_selected == null) return;
        if (LegacyLocator.RunningOldInstances().Count > 0
            && !Dialogs.Confirm(this, "旧バージョンが起動したままです。このまま取り込むと、これから旧バージョンで記録される分は取り込まれません。続けますか？"))
        {
            return;
        }
        if (_tracker.Settings.Migration != null
            && !Dialogs.Confirm(this, "既に取り込み済みです。もう一度取り込むと累計時間が二重になります。続けますか？"))
        {
            return;
        }

        var legacySettings = SettingsBox.IsChecked == true && _userConfig != null ? LegacyImporter.ReadUserConfig(_userConfig) : null;
        LegacyImportResult result;
        try
        {
            result = _tracker.ImportLegacy(_selected, legacySettings, _userConfig);
        }
        catch (Exception ex) when (Dialogs.IsSaveError(ex))
        {
            Dialogs.Warn(this, "取り込みに失敗しました。\n" + ex.Message);
            return;
        }

        var notes = new List<string>();
        try
        {
            // 旧バージョンと両方が自動起動すると二重に記録されるので、どちらか一方だけにする
            var settings = _tracker.Settings;
            if (AutoStartBox.Visibility == Visibility.Visible)
            {
                settings.AutoStart = AutoStartBox.IsChecked == true;
                if (settings.AutoStart)
                {
                    LegacyLocator.RemoveOldRunCommand();
                    notes.Add("自動起動をこのバージョンに切り替えました。");
                }
            }
            _tracker.UpdateSettings(settings);
            AutoStart.Set(settings.AutoStart);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or System.Security.SecurityException)
        {
            notes.Add("自動起動の設定に失敗しました: " + ex.Message);
        }

        if (legacySettings is { ApiKey.Length: > 0 })
        {
            try
            {
                await _toggl.ConnectAsync(legacySettings.ApiKey);
                notes.Add($"Toggl と連携しました ({_toggl.UserEmail})。");
            }
            catch (TogglApiException ex)
            {
                notes.Add("Toggl の API キーを引き継げませんでした。設定画面から入力し直してください: " + ex.Message);
            }
        }

        if (LegacyLocator.IsOldInstallerPresent())
        {
            notes.Add("旧バージョンはインストーラー版です。不要になったら Windows の「設定 → アプリ」から MHTimer をアンインストールしてください。" +
                "(旧バージョンのアンインストールでは旧バージョンのデータが削除されますが、取り込んだ記録と複製はこちらに残ります)");
        }

        Dialogs.Info(this,
            $"取り込みました。\n\nアプリ {result.AppsAdded} 件を追加 / 累計 {TimeText.FormatShort(result.Seconds)} / ファイル別の記録 {result.Files} 件\n" +
            $"元のデータの複製: {result.BackupDir}" +
            (notes.Count > 0 ? "\n\n" + string.Join("\n\n", notes) : ""));
        DialogResult = true;
    }

    private void OnSkip(object sender, RoutedEventArgs e)
    {
        _tracker.DismissLegacyPrompt();
        DialogResult = false;
    }
}
