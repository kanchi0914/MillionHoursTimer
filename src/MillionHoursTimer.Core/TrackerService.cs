using MillionHoursTimer.Core.Legacy;
using MillionHoursTimer.Core.Model;
using MillionHoursTimer.Core.Storage;
using MillionHoursTimer.Core.Toggl;
using MillionHoursTimer.Core.Tracking;

namespace MillionHoursTimer.Core;

public sealed record AppStatus(
    string Process,
    string Name,
    string ExePath,
    long TodaySeconds,
    long TotalSeconds,
    bool IsRecording,
    bool TogglEnabled,
    string TogglProject,
    string TogglTag,
    IReadOnlyList<string> Extensions,
    int FileExpireDays,
    int FileKeepMinutes);

public sealed record TrackerSnapshot(IReadOnlyList<AppStatus> Apps, bool IsIdle, int UnsavedCount);

public sealed record FileStatus(string Name, long TotalSeconds, DateOnly? LastDay, bool IsRecording, bool IsLocked);

public sealed record HistoryEntry(string Process, string Name, IReadOnlyDictionary<DateOnly, long> SecondsByDay)
{
    public long Total => SecondsByDay.Values.Sum();
}

public sealed record LegacyImportResult(int AppsAdded, long Seconds, int Files, string BackupDir);

/// <summary>
/// 計測全体のまとめ役。一定間隔で PC の状態を調べて計測し、記録の保存・集計・Toggl 送信を行う。
/// 公開メソッドはどのスレッドから呼んでもよい。
/// </summary>
public sealed class TrackerService : IDisposable
{
    private static readonly TimeSpan CheckpointInterval = TimeSpan.FromSeconds(10);

    private readonly object _gate = new();
    private readonly DataPaths _paths;
    private readonly ISystemProbe _probe;
    private readonly ILog _log;
    private readonly TogglService? _toggl;
    private readonly RecordStore _records;
    private readonly TrackingEngine _engine = new();
    private readonly List<(RecordKind Kind, SessionRecord Record)> _unsaved = [];

    private List<AppDefinition> _apps = [];
    private AppSettings _settings = new();
    private UsageStats _stats = new();
    private Dictionary<string, HashSet<string>> _locks = new(StringComparer.OrdinalIgnoreCase);
    private DateTime _lastCheckpoint = DateTime.MinValue;
    private DateOnly? _lastCleanupDay;
    private bool _unsavedWarned;

    private CancellationTokenSource? _loopCts;
    private Task? _loop;
    private FileSystemWatcher? _watcher;
    private Timer? _reloadTimer;

    public TrackerService(DataPaths paths, ISystemProbe probe, ILog log, TogglService? toggl)
    {
        _paths = paths;
        _probe = probe;
        _log = log;
        _toggl = toggl;
        _records = new RecordStore(paths);

        paths.EnsureCreated();
        lock (_gate)
        {
            _settings = SettingsStore.Load(paths.SettingsFile, Warnings);
            _apps = AppListStore.Load(paths.AppsFile, Warnings);
            _locks = FileLockStore.Load(paths.FileLocksFile, Warnings);
            RebuildStats();
            RecoverCheckpoint();
        }
        foreach (var w in Warnings) _log.Warn(w);
    }

    /// <summary>起動時に読み込めなかったデータなど、利用者に知らせたいこと</summary>
    public List<string> Warnings { get; } = [];

    public DataPaths Paths => _paths;

    /// <summary>計測のたびに呼ばれる (計測スレッドから呼ばれるので、画面側でスレッドを切り替えること)</summary>
    public event Action<TrackerSnapshot>? Updated;

    public AppSettings Settings
    {
        get
        {
            lock (_gate) return _settings.Clone();
        }
    }

    public bool IsFirstRun => !File.Exists(_paths.AppsFile) && !File.Exists(_paths.SettingsFile);

    #region 計測ループ

    public void Start()
    {
        if (_loop != null) return;
        _loopCts = new CancellationTokenSource();
        _loop = Task.Run(() => LoopAsync(_loopCts.Token));
        StartWatching();
    }

    private async Task LoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var started = DateTime.UtcNow;
            try
            {
                Tick();
            }
            catch (Exception ex)
            {
                _log.Error("計測中にエラーが発生しました", ex);
            }

            int interval;
            lock (_gate) interval = _settings.CountIntervalSeconds;
            var wait = TimeSpan.FromSeconds(interval) - (DateTime.UtcNow - started);
            if (wait < TimeSpan.FromMilliseconds(100)) wait = TimeSpan.FromMilliseconds(100);
            try
            {
                await Task.Delay(wait, ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    /// <summary>1回分の計測をする (通常は計測ループから呼ばれる)</summary>
    public void Tick()
    {
        HashSet<string> processes;
        lock (_gate) processes = _apps.Select(a => a.Process).ToHashSet(StringComparer.OrdinalIgnoreCase);

        // ウィンドウの列挙は時間がかかるので、ロックの外でやる
        var snapshot = _probe.Capture(processes);

        bool sendToggl;
        lock (_gate)
        {
            var finished = _engine.Tick(snapshot, _apps, _settings);
            sendToggl = HandleFinished(finished);
            if (finished.Count == 0) TrySaveUnsaved();
            // 起動直後と日付が変わったときに、古いファイル別の記録を消す
            var today = DateOnly.FromDateTime(snapshot.Now);
            if (_lastCleanupDay != today)
            {
                _lastCleanupDay = today;
                DeleteExpiredFiles(today);
            }
            if (finished.Count > 0 || (snapshot.Now - _lastCheckpoint).Duration() >= CheckpointInterval) WriteCheckpoint(snapshot.Now);
        }
        AfterChange(sendToggl);
    }

    /// <summary>スリープに入るときに呼ぶ。計測中の作業を全て終了する。</summary>
    public void Suspend()
    {
        bool sendToggl;
        lock (_gate)
        {
            sendToggl = HandleFinished(_engine.EndAll(null, _settings));
            WriteCheckpoint();
        }
        AfterChange(sendToggl);
    }

    /// <summary>終了するときに呼ぶ。計測中の作業を記録して止める。</summary>
    public void Shutdown()
    {
        _loopCts?.Cancel();
        try
        {
            _loop?.Wait(TimeSpan.FromSeconds(5));
        }
        catch (AggregateException)
        {
        }
        _loop = null;
        _watcher?.Dispose();
        _watcher = null;

        lock (_gate)
        {
            HandleFinished(_engine.EndAll(null, _settings));
            TrySaveUnsaved();
            if (_unsaved.Count == 0)
            {
                TryDelete(_paths.CheckpointFile);
            }
            else
            {
                WriteCheckpoint();
            }
        }
    }

    public void Dispose()
    {
        Shutdown();
        _reloadTimer?.Dispose();
    }

    public TrackerSnapshot GetSnapshot()
    {
        lock (_gate)
        {
            var today = DateOnly.FromDateTime(DateTime.Now);
            var apps = _apps.Select(a =>
            {
                var usage = _stats.Get(a.Process);
                var session = _engine.GetAppSession(a.Process);
                return new AppStatus(
                    a.Process,
                    a.DisplayName,
                    a.ExePath,
                    (usage?.SecondsOn(today) ?? 0) + (long)Math.Round(session?.SecondsOn(today) ?? 0),
                    (usage?.TotalSeconds ?? 0) + (long)Math.Round(session?.Seconds ?? 0),
                    _engine.IsRecording(a.Process),
                    a.TogglEnabled,
                    a.TogglProject,
                    a.TogglTag,
                    [.. a.Extensions],
                    a.FileExpireDays,
                    a.FileKeepMinutes);
            }).ToList();
            return new TrackerSnapshot(apps, _engine.IsIdle, _unsaved.Count);
        }
    }

    private void AfterChange(bool sendToggl)
    {
        if (sendToggl && _toggl != null) _ = _toggl.FlushAsync();
        Updated?.Invoke(GetSnapshot());
    }

    #endregion

    #region 記録の保存

    /// <returns>Toggl に送るものがあれば true</returns>
    private bool HandleFinished(List<FinishedSession> finished)
    {
        if (finished.Count == 0) return false;

        var sendToggl = false;
        foreach (var f in finished)
        {
            foreach (var r in f.Records)
            {
                _stats.Add(f.Kind, r);
                _unsaved.Add((f.Kind, r));
            }

            if (f.Kind == RecordKind.App && _toggl is { HasApiKey: true } && f.Seconds > _settings.TogglMinSeconds
                && FindApp(f.Process) is { TogglEnabled: true } app)
            {
                _toggl.Enqueue(new TogglEntry(f.SessionId, f.Start, f.Seconds, f.AppName, app.TogglProject, app.TogglTag));
                sendToggl = true;
            }
        }
        TrySaveUnsaved();
        return sendToggl;
    }

    /// <summary>まだファイルに書けていない記録を書く。Excel で開かれているなどで書けなければ次回に再試行する。</summary>
    private void TrySaveUnsaved()
    {
        if (_unsaved.Count == 0) return;

        foreach (var group in _unsaved.GroupBy(u => (u.Kind, Key: RecordStore.FileKeyOf(u.Record))).ToList())
        {
            try
            {
                _records.Append(group.Key.Kind, group.Select(g => g.Record).ToList());
                _unsaved.RemoveAll(u => u.Kind == group.Key.Kind && RecordStore.FileKeyOf(u.Record) == group.Key.Key);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                if (!_unsavedWarned) _log.Warn($"記録を保存できませんでした。あとで再試行します: {ex.Message}");
                _unsavedWarned = true;
            }
        }
        if (_unsaved.Count == 0 && _unsavedWarned)
        {
            _log.Info("保存できていなかった記録を保存しました");
            _unsavedWarned = false;
        }
    }

    private sealed class Checkpoint
    {
        public DateTime SavedAt { get; set; }
        public List<ActiveSession> Apps { get; set; } = [];
        public List<ActiveSession> Files { get; set; } = [];
        public List<SessionRecord> UnsavedApps { get; set; } = [];
        public List<SessionRecord> UnsavedFiles { get; set; } = [];
    }

    /// <summary>計測中の作業を current.json に書いておく (異常終了したときの復旧用)</summary>
    private void WriteCheckpoint(DateTime? now = null)
    {
        try
        {
            JsonFiles.Write(_paths.CheckpointFile, new Checkpoint
            {
                SavedAt = now ?? DateTime.Now,
                Apps = [.. _engine.AppSessions],
                Files = [.. _engine.FileSessions],
                UnsavedApps = _unsaved.Where(u => u.Kind == RecordKind.App).Select(u => u.Record).ToList(),
                UnsavedFiles = _unsaved.Where(u => u.Kind == RecordKind.File).Select(u => u.Record).ToList(),
            });
            _lastCheckpoint = now ?? DateTime.Now;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _log.Warn($"current.json を書けませんでした: {ex.Message}");
        }
    }

    /// <summary>前回が異常終了だった場合、current.json に残っている作業を記録に加える</summary>
    private void RecoverCheckpoint()
    {
        Checkpoint? cp;
        try
        {
            cp = JsonFiles.Read<Checkpoint>(_paths.CheckpointFile);
        }
        catch (Exception ex) when (ex is IOException or System.Text.Json.JsonException)
        {
            _log.Error("current.json を読めませんでした", ex);
            return;
        }
        if (cp == null) return;

        var finished = new List<FinishedSession>();
        finished.AddRange(cp.Apps.Select(s => TrackingEngine.Finish(RecordKind.App, s, null, _settings.MinSecondsFor(RecordKind.App))).OfType<FinishedSession>());
        finished.AddRange(cp.Files.Select(s => TrackingEngine.Finish(RecordKind.File, s, null, _settings.MinSecondsFor(RecordKind.File))).OfType<FinishedSession>());
        foreach (var r in cp.UnsavedApps)
        {
            _stats.Add(RecordKind.App, r);
            _unsaved.Add((RecordKind.App, r));
        }
        foreach (var r in cp.UnsavedFiles)
        {
            _stats.Add(RecordKind.File, r);
            _unsaved.Add((RecordKind.File, r));
        }

        var count = finished.Count + cp.UnsavedApps.Count + cp.UnsavedFiles.Count;
        HandleFinished(finished);
        TrySaveUnsaved();
        // 書き終えた分が二重に記録されないよう、すぐに current.json を更新する
        WriteCheckpoint();
        if (count > 0) _log.Info($"前回終了時に保存されていなかった記録を {count} 件復旧しました ({cp.SavedAt:yyyy-MM-dd HH:mm:ss} 時点)");
    }

    private void RebuildStats()
    {
        var stats = new UsageStats();
        stats.AddRange(RecordKind.App, _records.LoadAll(RecordKind.App, Warnings));
        stats.AddRange(RecordKind.File, _records.LoadAll(RecordKind.File, Warnings));
        foreach (var (kind, r) in _unsaved) stats.Add(kind, r);
        _stats = stats;
    }

    #endregion

    #region 外部での編集の反映

    private void StartWatching()
    {
        try
        {
            _reloadTimer = new Timer(_ => ReloadFromDisk(), null, Timeout.Infinite, Timeout.Infinite);
            _watcher = new FileSystemWatcher(_paths.Root)
            {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size,
            };
            FileSystemEventHandler handler = (_, e) => OnFileChanged(e.FullPath);
            _watcher.Changed += handler;
            _watcher.Created += handler;
            _watcher.Deleted += handler;
            _watcher.Renamed += (_, e) => OnFileChanged(e.FullPath);
            _watcher.EnableRaisingEvents = true;
        }
        catch (Exception ex) when (ex is IOException or ArgumentException)
        {
            _log.Warn($"データフォルダの変更を監視できません: {ex.Message}");
        }
    }

    private void OnFileChanged(string path)
    {
        var relevant = string.Equals(path, _paths.AppsFile, StringComparison.OrdinalIgnoreCase)
            || string.Equals(path, _paths.SettingsFile, StringComparison.OrdinalIgnoreCase)
            || string.Equals(path, _paths.FileLocksFile, StringComparison.OrdinalIgnoreCase)
            || (path.StartsWith(_paths.RecordsDir, StringComparison.OrdinalIgnoreCase)
                && path.EndsWith(".csv", StringComparison.OrdinalIgnoreCase));
        // 連続した変更をまとめて1回だけ読み直す
        if (relevant) _reloadTimer?.Change(TimeSpan.FromSeconds(2), Timeout.InfiniteTimeSpan);
    }

    /// <summary>テキストエディタや Excel で編集された内容を読み直す</summary>
    public void ReloadFromDisk()
    {
        bool sendToggl;
        lock (_gate)
        {
            // 全て読めたときだけ差し替える (開けないファイルがあると、合計が減って見えたりするため)
            var warnings = new List<string>();
            AppSettings settings;
            List<AppDefinition> apps;
            Dictionary<string, HashSet<string>> locks;
            List<SessionRecord> appRecords, fileRecords;
            try
            {
                settings = SettingsStore.Load(_paths.SettingsFile, warnings);
                apps = AppListStore.Load(_paths.AppsFile, warnings);
                locks = FileLockStore.Load(_paths.FileLocksFile, warnings);
                appRecords = _records.LoadAll(RecordKind.App, warnings, strict: true);
                fileRecords = _records.LoadAll(RecordKind.File, warnings, strict: true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Excel で開かれていてロックされているなど。少し待ってから読み直す
                _log.Warn($"データを読み直せませんでした。あとで再試行します: {ex.Message}");
                _reloadTimer?.Change(TimeSpan.FromSeconds(5), Timeout.InfiniteTimeSpan);
                return;
            }
            foreach (var w in warnings) _log.Warn(w);

            var finished = new List<FinishedSession>();
            foreach (var removed in _apps.Where(a => apps.All(n => !SameProcess(n.Process, a.Process))))
            {
                finished.AddRange(_engine.EndApp(removed.Process, settings));
            }
            // 自動削除の設定が手で書き換えられたら、日付が変わるのを待たずに反映する
            if (apps.Any(a => FindApp(a.Process) is not { } old
                    || old.FileExpireDays != a.FileExpireDays || old.FileKeepMinutes != a.FileKeepMinutes))
            {
                _lastCleanupDay = null;
            }
            _settings = settings;
            _apps = apps;
            _locks = locks;
            var stats = new UsageStats();
            stats.AddRange(RecordKind.App, appRecords);
            stats.AddRange(RecordKind.File, fileRecords);
            foreach (var (kind, r) in _unsaved) stats.Add(kind, r);
            _stats = stats;
            sendToggl = HandleFinished(finished);
        }
        AfterChange(sendToggl);
    }

    #endregion

    #region アプリの管理

    private AppDefinition? FindApp(string process) => _apps.FirstOrDefault(a => SameProcess(a.Process, process));

    private static bool SameProcess(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    private void SaveApps() => AppListStore.Save(_paths.AppsFile, _apps);

    public bool HasApp(string process)
    {
        lock (_gate) return FindApp(process) != null;
    }

    /// <returns>追加したら true、既に登録されていたら false</returns>
    public bool AddApp(string process, string name, string exePath)
    {
        lock (_gate)
        {
            if (FindApp(process) != null) return false;
            _apps.Add(new AppDefinition { Process = process, Name = name, ExePath = exePath });
            SaveApps();
        }
        AfterChange(false);
        return true;
    }

    /// <summary>アプリの設定を変更する (表示名・拡張子・Toggl 設定)</summary>
    public void UpdateApp(string process, Action<AppDefinition> update)
    {
        lock (_gate)
        {
            var app = FindApp(process) ?? throw new InvalidOperationException($"{process} は登録されていません");
            var backup = app.Clone();
            update(app);
            try
            {
                SaveApps();
            }
            catch
            {
                _apps[_apps.IndexOf(app)] = backup;
                throw;
            }
        }
        AfterChange(false);
    }

    /// <param name="deleteRecords">true なら作業記録も削除する。false なら一覧から外すだけで、再登録すると記録が戻る。</param>
    public void RemoveApp(string process, bool deleteRecords)
    {
        bool sendToggl = false;
        lock (_gate)
        {
            var app = FindApp(process);
            if (app == null) return;

            var finished = _engine.EndApp(process, _settings);
            _apps.Remove(app);
            SaveApps();

            if (deleteRecords)
            {
                _unsaved.RemoveAll(u => SameProcess(u.Record.Process, process));
                _records.Rewrite(RecordKind.App, r => SameProcess(r.Process, process) ? null : r);
                _records.Rewrite(RecordKind.File, r => SameProcess(r.Process, process) ? null : r);
                TryDelete(_paths.IconFile(process));
                TryChangeLocks(locks => locks.Remove(process));
            }
            else
            {
                sendToggl = HandleFinished(finished);
            }
            RebuildStats();
            WriteCheckpoint();
        }
        AfterChange(sendToggl);
    }

    public IReadOnlyList<FileStatus> GetFiles(string process)
    {
        lock (_gate)
        {
            var files = new Dictionary<string, FileStatus>(StringComparer.Ordinal);
            var locked = _locks.GetValueOrDefault(process);
            bool IsLocked(string file) => locked?.Contains(file) == true;
            if (_stats.Get(process) is { } usage)
            {
                foreach (var f in usage.Files.Values) files[f.Name] = new FileStatus(f.Name, f.TotalSeconds, f.LastDay, false, IsLocked(f.Name));
            }
            foreach (var s in _engine.GetFileSessions(process))
            {
                var today = DateOnly.FromDateTime(s.LastSeen);
                var seconds = (long)Math.Round(s.Seconds);
                files[s.File] = files.TryGetValue(s.File, out var f)
                    ? f with { TotalSeconds = f.TotalSeconds + seconds, LastDay = today, IsRecording = s.SeenInLastTick }
                    : new FileStatus(s.File, seconds, today, s.SeenInLastTick, IsLocked(s.File));
            }
            return files.Values.ToList();
        }
    }

    /// <summary>複数のファイルの記録を1つの名前にまとめる</summary>
    public void MergeFiles(string process, IReadOnlyCollection<string> files, string newName)
    {
        var targets = files.ToHashSet(StringComparer.Ordinal);
        lock (_gate)
        {
            // 計測中の分も記録してから名前を変える
            HandleFinished(_engine.EndFiles(process, targets, _settings));
            SessionRecord Rename(SessionRecord r) =>
                SameProcess(r.Process, process) && targets.Contains(r.File) ? r with { File = newName } : r;

            for (var i = 0; i < _unsaved.Count; i++)
            {
                if (_unsaved[i].Kind == RecordKind.File) _unsaved[i] = (RecordKind.File, Rename(_unsaved[i].Record));
            }
            _records.Rewrite(RecordKind.File, Rename);
            RebuildStats();
            WriteCheckpoint();

            // どれか1つでもロックしていたら、まとめた後の項目もロックする
            if (_locks.GetValueOrDefault(process) is { } locked && locked.Overlaps(targets))
            {
                ChangeLocks(locks =>
                {
                    locks[process].ExceptWith(targets);
                    locks[process].Add(newName);
                });
            }
        }
        AfterChange(false);
    }

    public void DeleteFiles(string process, IReadOnlyCollection<string> files)
    {
        var targets = files.ToHashSet(StringComparer.Ordinal);
        lock (_gate)
        {
            RemoveFileRecords(process, targets);
            RebuildStats();
            WriteCheckpoint();
            if (_locks.GetValueOrDefault(process) is { } locked && locked.Overlaps(targets))
            {
                TryChangeLocks(locks => locks[process].ExceptWith(targets));
            }
        }
        AfterChange(false);
    }

    private void RemoveFileRecords(string process, IReadOnlySet<string> targets)
    {
        _engine.DiscardFiles(process, targets);
        bool Match(SessionRecord r) => SameProcess(r.Process, process) && targets.Contains(r.File);
        _unsaved.RemoveAll(u => u.Kind == RecordKind.File && Match(u.Record));
        _records.Rewrite(RecordKind.File, r => Match(r) ? null : r);
    }

    /// <summary>ロックした記録は自動削除と「全て削除」の対象にしない</summary>
    public void SetFilesLocked(string process, IReadOnlyCollection<string> files, bool locked)
    {
        lock (_gate)
        {
            ChangeLocks(locks =>
            {
                if (!locks.TryGetValue(process, out var set))
                {
                    set = new HashSet<string>(StringComparer.Ordinal);
                    locks[process] = set;
                }
                if (locked) set.UnionWith(files);
                else set.ExceptWith(files);
                if (set.Count == 0) locks.Remove(process);
            });
        }
        AfterChange(false);
    }

    /// <summary>ロック一覧を変更して保存する。保存できなければ元に戻して例外を投げる。</summary>
    private void ChangeLocks(Action<Dictionary<string, HashSet<string>>> change)
    {
        var next = _locks.ToDictionary(p => p.Key, p => new HashSet<string>(p.Value, StringComparer.Ordinal), StringComparer.OrdinalIgnoreCase);
        change(next);
        FileLockStore.Save(_paths.FileLocksFile, next);
        _locks = next;
    }

    /// <summary>記録を消した後の後片付け用。ロックが残っても害はないので、保存できなくても止めない。</summary>
    private void TryChangeLocks(Action<Dictionary<string, HashSet<string>>> change)
    {
        try
        {
            ChangeLocks(change);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _log.Warn($"file_locks.csv を更新できませんでした: {ex.Message}");
        }
    }

    /// <summary>ファイル別の記録の自動削除を設定し、条件に当てはまる記録をすぐに削除する</summary>
    /// <returns>削除した件数</returns>
    public int SetFileCleanup(string process, int expireDays, int keepMinutes)
    {
        int deleted;
        lock (_gate)
        {
            var app = FindApp(process) ?? throw new InvalidOperationException($"{process} は登録されていません");
            var backup = app.Clone();
            app.FileExpireDays = Math.Max(0, expireDays);
            app.FileKeepMinutes = Math.Max(0, keepMinutes);
            try
            {
                SaveApps();
            }
            catch
            {
                _apps[_apps.IndexOf(app)] = backup;
                throw;
            }
            var expired = ExpiredFiles(app, DateOnly.FromDateTime(DateTime.Now));
            if (expired.Count > 0)
            {
                RemoveFileRecords(process, expired);
                RebuildStats();
                WriteCheckpoint();
                _log.Info($"{app.DisplayName}: 古いファイル別の記録を {expired.Count}件 削除しました");
            }
            deleted = expired.Count;
        }
        AfterChange(false);
        return deleted;
    }

    /// <summary>自動削除をこの設定にしたら、いま削除される件数</summary>
    public int CountExpiredFiles(string process, int expireDays, int keepMinutes)
    {
        lock (_gate)
        {
            if (FindApp(process) is not { } app) return 0;
            var trial = app.Clone();
            trial.FileExpireDays = expireDays;
            trial.FileKeepMinutes = keepMinutes;
            return ExpiredFiles(trial, DateOnly.FromDateTime(DateTime.Now)).Count;
        }
    }

    /// <summary>
    /// 自動削除の対象になるファイル。ロックしたもの・計測中のもの・旧バージョンから取り込んだ日付のない記録は対象にしない。
    /// </summary>
    private HashSet<string> ExpiredFiles(AppDefinition app, DateOnly today)
    {
        var expired = new HashSet<string>(StringComparer.Ordinal);
        if (app.FileExpireDays <= 0 || _stats.Get(app.Process) is not { } usage) return expired;

        var locked = _locks.GetValueOrDefault(app.Process);
        var active = _engine.GetFileSessions(app.Process).Select(s => s.File).ToHashSet(StringComparer.Ordinal);
        foreach (var f in usage.Files.Values)
        {
            if (f.LastDay is not { } last || today.DayNumber - last.DayNumber < app.FileExpireDays) continue;
            if (app.FileKeepMinutes > 0 && f.TotalSeconds >= app.FileKeepMinutes * 60L) continue;
            if (locked?.Contains(f.Name) == true || active.Contains(f.Name)) continue;
            expired.Add(f.Name);
        }
        return expired;
    }

    private void DeleteExpiredFiles(DateOnly today)
    {
        var deletedAny = false;
        foreach (var app in _apps)
        {
            var expired = ExpiredFiles(app, today);
            if (expired.Count == 0) continue;
            try
            {
                RemoveFileRecords(app.Process, expired);
                deletedAny = true;
                _log.Info($"{app.DisplayName}: 古いファイル別の記録を {expired.Count}件 削除しました");
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // 記録ファイルが Excel で開かれているなど。次に日付が変わったとき (か次の起動時) にやり直す
                _log.Warn($"{app.DisplayName}: 古いファイル別の記録を削除できませんでした: {ex.Message}");
            }
        }
        if (!deletedAny) return;
        RebuildStats();
        WriteCheckpoint();
    }

    public void UpdateSettings(AppSettings settings)
    {
        lock (_gate)
        {
            var next = settings.Clone();
            next.Normalize();
            next.Migration = _settings.Migration;
            SettingsStore.Save(_paths.SettingsFile, next);
            _settings = next;
        }
        AfterChange(false);
    }

    /// <summary>日ごと・アプリごとの作業時間 (一覧から外したアプリも含む)</summary>
    public IReadOnlyList<HistoryEntry> GetHistory(DateOnly from, DateOnly to)
    {
        lock (_gate)
        {
            var result = new List<HistoryEntry>();
            var processes = _stats.Apps.Keys.Concat(_engine.AppSessions.Select(s => s.Process))
                .Distinct(StringComparer.OrdinalIgnoreCase);
            foreach (var process in processes)
            {
                var days = new Dictionary<DateOnly, long>();
                var usage = _stats.Get(process);
                if (usage != null)
                {
                    foreach (var (day, sec) in usage.ByDay.Where(d => d.Key >= from && d.Key <= to)) days[day] = sec;
                }
                if (_engine.GetAppSession(process) is { } session)
                {
                    foreach (var (day, sec) in session.SecondsByDay.Where(d => d.Key >= from && d.Key <= to))
                    {
                        days[day] = days.GetValueOrDefault(day) + (long)Math.Round(sec);
                    }
                }
                if (days.Count == 0) continue;

                var name = FindApp(process)?.DisplayName ?? (usage?.LastAppName is { Length: > 0 } n ? n : process);
                result.Add(new HistoryEntry(process, name, days));
            }
            return result.OrderByDescending(h => h.Total).ToList();
        }
    }

    private void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _log.Warn($"{path} を削除できませんでした: {ex.Message}");
        }
    }

    #endregion

    #region 旧バージョンからの移行

    /// <summary>
    /// 旧バージョンのデータを取り込む。旧データは変更しない。
    /// 累計時間は「いつ作業したか」が残っていないため、日付なしの行として legacy.csv に入れる。
    /// 旧インストーラーはアンインストール時に data フォルダを消すので、元のファイルも丸ごと legacy-backup に複製しておく。
    /// </summary>
    public LegacyImportResult ImportLegacy(LegacyData data, LegacySettings? legacySettings, string? userConfigPath = null)
    {
        LegacyImportResult result;
        lock (_gate)
        {
            var backupDir = Path.Combine(_paths.Root, "legacy-backup", DateTime.Now.ToString("yyyyMMdd-HHmmss"));
            CopyDirectory(data.DataDir, Path.Combine(backupDir, "data"));
            if (userConfigPath != null && File.Exists(userConfigPath))
            {
                File.Copy(userConfigPath, Path.Combine(backupDir, "user.config"));
            }

            var added = 0;
            foreach (var legacy in data.Apps)
            {
                var app = FindApp(legacy.Process);
                if (app == null)
                {
                    _apps.Add(new AppDefinition
                    {
                        Process = legacy.Process,
                        Name = legacy.Process,
                        Extensions = [.. legacy.Extensions],
                        TogglEnabled = legacy.TogglEnabled,
                        TogglProject = legacy.TogglProject,
                        TogglTag = legacy.TogglTag,
                    });
                    added++;
                }
                else if (app.Extensions.Count == 0)
                {
                    app.Extensions = [.. legacy.Extensions];
                }

                var icon = _paths.IconFile(legacy.Process);
                if (legacy.IconPath != null && !File.Exists(icon)) File.Copy(legacy.IconPath, icon);
            }

            var appRecords = data.Apps.Where(a => a.TotalSeconds > 0)
                .Select(a => new SessionRecord { Seconds = a.TotalSeconds, Process = a.Process, AppName = a.Process, SessionId = "legacy" })
                .ToList();
            var fileRecords = data.Apps.SelectMany(a => a.Files.Select(f => new SessionRecord
            {
                Seconds = f.Seconds,
                Process = a.Process,
                AppName = a.Process,
                File = f.Name,
                SessionId = "legacy",
            })).ToList();
            if (appRecords.Count > 0) _records.Append(RecordKind.App, appRecords);
            if (fileRecords.Count > 0) _records.Append(RecordKind.File, fileRecords);
            SaveApps();

            legacySettings?.ApplyTo(_settings);
            _settings.Migration = new LegacyMigrationInfo { SourceDir = data.DataDir, MigratedAt = DateTime.Now };
            SettingsStore.Save(_paths.SettingsFile, _settings);

            RebuildStats();
            result = new LegacyImportResult(added, appRecords.Sum(r => r.Seconds), fileRecords.Count, backupDir);
            _log.Info($"旧バージョンのデータを取り込みました: {data.DataDir} (アプリ {added} 件追加, ファイル {fileRecords.Count} 件, 複製先 {backupDir})");
        }
        AfterChange(false);
        return result;
    }

    /// <summary>旧バージョンの取り込みを案内しないようにする</summary>
    public void DismissLegacyPrompt()
    {
        lock (_gate)
        {
            _settings.SkipLegacyPrompt = true;
            SettingsStore.Save(_paths.SettingsFile, _settings);
        }
    }

    private static void CopyDirectory(string source, string dest)
    {
        Directory.CreateDirectory(dest);
        foreach (var file in Directory.GetFiles(source)) File.Copy(file, Path.Combine(dest, Path.GetFileName(file)));
        foreach (var dir in Directory.GetDirectories(source)) CopyDirectory(dir, Path.Combine(dest, Path.GetFileName(dir)));
    }

    #endregion
}
