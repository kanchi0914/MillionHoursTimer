using System.Net.Http;
using System.Reflection;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Win32;
using MillionHoursTimer.Core;
using MillionHoursTimer.Core.Storage;
using MillionHoursTimer.Core.Toggl;
using MillionHoursTimer.Platform;
using MillionHoursTimer.UI;

namespace MillionHoursTimer;

public partial class App : Application
{
    private readonly string[] _args;
    private readonly SingleInstance _instance;
    private ILog _log = NullLog.Instance;
    private TrackerService? _tracker;
    private TogglService? _toggl;
    private TrayIcon? _tray;
    private MainWindow? _main;
    private DispatcherTimer? _togglTimer;
    private bool _stopped;

    internal App(string[] args, SingleInstance instance)
    {
        _args = args;
        _instance = instance;
    }

    public static string Version =>
        Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0]
        ?? "?";

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var paths = GetDataDirArg() is { } dir ? new DataPaths(dir) : DataPaths.Default();
        paths.EnsureCreated();
        _log = new FileLog(paths.LogsDir);
        _log.Info($"起動しました (v{Version}, データ: {paths.Root})");
        DispatcherUnhandledException += OnUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += (_, ex) => _log.Error("予期しないエラー", ex.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, ex) =>
        {
            _log.Error("予期しないエラー (バックグラウンド)", ex.Exception);
            ex.SetObserved();
        };

        if (!ConfirmOldVersionStopped())
        {
            Shutdown();
            return;
        }

        var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        _toggl = new TogglService(new TogglClient(http), new TogglQueue(paths.TogglLogFile),
            new DpapiSecretStore(paths.TogglKeyFile, _log), _log);
        _tracker = new TrackerService(paths, new WindowsSystemProbe(), _log, _toggl);
        var firstRun = _tracker.IsFirstRun;
        var updater = new Updater(_log);

        _main = new MainWindow(_tracker, _toggl, updater, _log, RequestExit, StopTracking);
        _tray = new TrayIcon(ShowMain, RequestExit);
        _tracker.Updated += s => Dispatcher.BeginInvoke(() => UpdateTrayText(s));
        _instance.OnSecondInstance(() => Dispatcher.BeginInvoke(ShowMain));
        SystemEvents.PowerModeChanged += OnPowerModeChanged;

        SyncAutoStart();
        _tracker.Start();

        if (!_args.Contains(AutoStart.MinimizedArg)) ShowMain();
        if (_tracker.Warnings.Count > 0)
        {
            Dialogs.Warn(null, "データの一部を読み込めませんでした。詳しくは logs フォルダのログを確認してください。\n\n"
                + string.Join("\n", _tracker.Warnings.Take(8)) + (_tracker.Warnings.Count > 8 ? "\n…" : ""));
        }

        var settings = _tracker.Settings;
        if (settings.Migration == null && !settings.SkipLegacyPrompt && (firstRun || LegacyLocator.FindDataDirs().Count > 0))
        {
            _main.ShowMigration(firstRun);
        }

        _ = StartBackgroundAsync(updater);
        _togglTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(15) };
        _togglTimer.Tick += (_, _) => _ = _toggl.FlushAsync();
        _togglTimer.Start();
    }

    private string? GetDataDirArg()
    {
        var i = Array.IndexOf(_args, "--data-dir");
        return i >= 0 && i + 1 < _args.Length ? _args[i + 1] : null;
    }

    private async Task StartBackgroundAsync(Updater updater)
    {
        await _toggl!.ConnectAsync();
        await _toggl.FlushAsync();
        if (await updater.CheckAsync() is { } version) _main?.NotifyUpdate(version);
    }

    private void ShowMain() => _main?.ShowAndActivate();

    private string _trayText = "";

    private void UpdateTrayText(TrackerSnapshot s)
    {
        var recording = s.Apps.Where(a => a.IsRecording).Select(a => a.Name).ToList();
        var text = s.IsIdle ? "MillionHoursTimer (無操作のため停止中)"
            : recording.Count > 0 ? "記録中: " + string.Join(", ", recording)
            : "MillionHoursTimer";
        if (text == _trayText || _tray == null) return;
        _trayText = text;
        _tray.Text = text;
    }

    private void SyncAutoStart()
    {
        try
        {
            var enabled = _tracker!.Settings.AutoStart;
            if (enabled != AutoStart.IsEnabled()) AutoStart.Set(enabled);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException or System.IO.IOException)
        {
            _log.Warn($"自動起動の設定を確認できませんでした: {ex.Message}");
        }
    }

    /// <summary>旧バージョンと両方で記録すると二重になるので、起動していたら終了を勧める</summary>
    private bool ConfirmOldVersionStopped()
    {
        if (LegacyLocator.RunningOldInstances().Count == 0) return true;
        var answer = MessageBox.Show(
            "旧バージョン (MHTimer) が起動しています。\n両方で記録すると時間が二重に数えられるため、旧バージョンを終了することをおすすめします。\n\n" +
            "［はい］旧バージョンを終了して起動する\n［いいえ］そのまま起動する\n［キャンセル］起動をやめる",
            "MillionHoursTimer", MessageBoxButton.YesNoCancel, MessageBoxImage.Warning, MessageBoxResult.Yes);
        if (answer == MessageBoxResult.Cancel) return false;
        if (answer == MessageBoxResult.Yes) StopOldInstances(null);
        return true;
    }

    /// <summary>
    /// 旧バージョンを終了させる。旧バージョンは×ボタンでは終了せず、記録も毎秒保存しているので、プロセスを終了させる。
    /// </summary>
    internal static void StopOldInstances(Window? owner)
    {
        foreach (var p in LegacyLocator.RunningOldInstances())
        {
            using (p)
            {
                try
                {
                    p.Kill();
                    p.WaitForExit(5000);
                }
                catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
                {
                    Dialogs.Warn(owner, "旧バージョンを終了できませんでした。タスクトレイのアイコンを右クリックして「終了」してください。\n" + ex.Message);
                }
            }
        }
    }

    private void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode == PowerModes.Suspend)
        {
            _log.Info("スリープに入ります");
            _tracker?.Suspend();
        }
        else if (e.Mode == PowerModes.Resume)
        {
            _log.Info("スリープから復帰しました");
        }
    }

    private void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        _log.Error("予期しないエラー", e.Exception);
        Dialogs.Warn(null, "予期しないエラーが発生しました。記録は続けています。\n\n" + e.Exception.Message);
        e.Handled = true;
    }

    protected override void OnSessionEnding(SessionEndingCancelEventArgs e)
    {
        base.OnSessionEnding(e);
        _log.Info("Windows の終了を検知しました");
        StopTracking();
    }

    private bool _confirmingExit;

    private void RequestExit()
    {
        if (_confirmingExit) return;
        _confirmingExit = true;
        try
        {
            if (!Dialogs.Confirm(_main?.IsVisible == true ? _main : null, "MillionHoursTimer を終了すると記録が止まります。終了しますか？")) return;
        }
        finally
        {
            _confirmingExit = false;
        }
        StopTracking();
        Shutdown();
    }

    /// <summary>計測中の作業を保存して止める (終了・再起動の前に呼ぶ)</summary>
    private void StopTracking()
    {
        if (_stopped) return;
        _stopped = true;
        SystemEvents.PowerModeChanged -= OnPowerModeChanged;
        _togglTimer?.Stop();
        _tracker?.Shutdown();
        if (_toggl != null)
        {
            // 送りきれなかったものは次回起動時に送る
            try
            {
                Task.Run(() => _toggl.FlushAsync()).Wait(TimeSpan.FromSeconds(3));
            }
            catch (AggregateException)
            {
            }
        }
        _tray?.Dispose();
        if (_main != null) _main.AllowClose = true;
        _log.Info("終了しました");
    }

    protected override void OnExit(ExitEventArgs e)
    {
        StopTracking();
        base.OnExit(e);
    }
}
