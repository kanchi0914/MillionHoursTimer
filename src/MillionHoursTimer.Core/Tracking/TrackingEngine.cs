using System.Text.Json.Serialization;
using MillionHoursTimer.Core.Model;

namespace MillionHoursTimer.Core.Tracking;

/// <summary>計測中の作業1つ分 (アプリ単位、またはファイル単位)</summary>
public sealed class ActiveSession
{
    public string SessionId { get; set; } = "";
    public string Process { get; set; } = "";
    public string AppName { get; set; } = "";
    public string File { get; set; } = "";
    public DateTime Start { get; set; }
    public DateTime LastSeen { get; set; }

    /// <summary>日付ごとの計測秒数。日付をまたいだ作業を日ごとの行に分けるのに使う。</summary>
    public Dictionary<DateOnly, double> SecondsByDay { get; set; } = [];

    /// <summary>直近の計測で対象になっていたか (画面で「記録中」と表示する)</summary>
    [JsonIgnore]
    public bool SeenInLastTick { get; set; }

    [JsonIgnore]
    public double Seconds => SecondsByDay.Values.Sum();

    public double SecondsOn(DateOnly day) => SecondsByDay.GetValueOrDefault(day);
}

/// <summary>終了した作業。Records は日付ごとに分けた保存用の行。</summary>
public sealed record FinishedSession(
    RecordKind Kind,
    string SessionId,
    string Process,
    string AppName,
    string File,
    DateTime Start,
    DateTime End,
    long Seconds,
    IReadOnlyList<SessionRecord> Records);

/// <summary>
/// 計測エンジン。PC の状態を一定間隔で受け取り、作業の開始・継続・終了を判定する。
/// スレッドセーフではないので、呼び出し側で排他すること。
/// </summary>
public sealed class TrackingEngine
{
    /// <summary>一時的に対象外になっても、この時間内に戻れば同じ作業として扱う (その間の時間は数えない)</summary>
    public static readonly TimeSpan Grace = TimeSpan.FromSeconds(60);

    private readonly Dictionary<string, ActiveSession> _apps = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ActiveSession> _files = new(StringComparer.Ordinal);
    private DateTime? _lastTick;

    public bool IsIdle { get; private set; }
    public IReadOnlyCollection<ActiveSession> AppSessions => _apps.Values;
    public IReadOnlyCollection<ActiveSession> FileSessions => _files.Values;

    public static string FileKey(string process, string file) => process.ToUpperInvariant() + "\0" + file;

    public bool IsRecording(string process) => _apps.TryGetValue(process, out var s) && s.SeenInLastTick;

    public ActiveSession? GetAppSession(string process) => _apps.GetValueOrDefault(process);

    public IEnumerable<ActiveSession> GetFileSessions(string process) =>
        _files.Values.Where(f => string.Equals(f.Process, process, StringComparison.OrdinalIgnoreCase));

    public List<FinishedSession> Tick(SystemSnapshot snapshot, IReadOnlyList<AppDefinition> apps, AppSettings settings)
    {
        var finished = new List<FinishedSession>();
        var now = snapshot.Now;

        // 時計が戻った場合は、続きとして扱わず区切る
        if (_lastTick is { } prev && now < prev) finished.AddRange(EndAll(null, settings));
        _lastTick = now;

        if (settings.IdleTimeoutSeconds > 0 && snapshot.IdleSeconds >= settings.IdleTimeoutSeconds)
        {
            // 最後に操作した時点で作業が終わったものとする
            IsIdle = true;
            finished.AddRange(EndAll(now.AddSeconds(-snapshot.IdleSeconds), settings));
            return finished;
        }
        IsIdle = false;

        var tolerance = TimeSpan.FromSeconds(settings.CountIntervalSeconds * 2 + 2);
        var grace = tolerance > Grace ? tolerance : Grace;
        var seenApps = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenFiles = new HashSet<string>(StringComparer.Ordinal);

        foreach (var app in apps)
        {
            if (!IsCounted(app, snapshot, settings.CountMode)) continue;
            seenApps.Add(app.Process);
            Touch(_apps, app.Process, app, "", now, tolerance);

            if (app.Extensions.Count == 0 && !settings.UseHyphenFileName) continue;
            foreach (var title in FileTitles(app, snapshot, settings))
            {
                var name = FileNameExtractor.Extract(title, app.Extensions, settings.SplitFileNameBySpace, settings.UseHyphenFileName);
                if (name == null) continue;
                var key = FileKey(app.Process, name);
                if (!seenFiles.Add(key)) continue;
                Touch(_files, key, app, name, now, tolerance);
            }
        }

        Expire(_apps, seenApps, now, grace, settings, RecordKind.App, finished);
        Expire(_files, seenFiles, now, grace, settings, RecordKind.File, finished);
        return finished;
    }

    /// <summary>全ての作業を終了する (スリープ・終了時など)。cutoff を指定するとその時刻以降を切り捨てる。</summary>
    public List<FinishedSession> EndAll(DateTime? cutoff, AppSettings settings)
    {
        var finished = new List<FinishedSession>();
        foreach (var s in _apps.Values)
        {
            if (Finish(RecordKind.App, s, cutoff, settings.MinRecordSeconds) is { } f) finished.Add(f);
        }
        foreach (var s in _files.Values)
        {
            if (Finish(RecordKind.File, s, cutoff, settings.MinRecordSeconds) is { } f) finished.Add(f);
        }
        _apps.Clear();
        _files.Clear();
        return finished;
    }

    /// <summary>特定のアプリの作業を終了する (一覧から削除したときなど)</summary>
    public List<FinishedSession> EndApp(string process, AppSettings settings)
    {
        var finished = new List<FinishedSession>();
        if (_apps.Remove(process, out var app)
            && Finish(RecordKind.App, app, null, settings.MinRecordSeconds) is { } f)
        {
            finished.Add(f);
        }
        foreach (var (key, file) in _files.Where(p => string.Equals(p.Value.Process, process, StringComparison.OrdinalIgnoreCase)).ToList())
        {
            _files.Remove(key);
            if (Finish(RecordKind.File, file, null, settings.MinRecordSeconds) is { } ff) finished.Add(ff);
        }
        return finished;
    }

    /// <summary>計測中のファイル単位の作業を終了する (ファイルの記録をまとめるときなど)</summary>
    public List<FinishedSession> EndFiles(string process, IReadOnlySet<string> files, AppSettings settings)
    {
        var finished = new List<FinishedSession>();
        foreach (var file in files)
        {
            if (_files.Remove(FileKey(process, file), out var s)
                && Finish(RecordKind.File, s, null, settings.MinRecordSeconds) is { } f)
            {
                finished.Add(f);
            }
        }
        return finished;
    }

    /// <summary>計測中のファイル単位の作業を記録せずに捨てる (ファイルの記録を削除したときなど)</summary>
    public void DiscardFiles(string process, IReadOnlySet<string> files)
    {
        foreach (var file in files) _files.Remove(FileKey(process, file));
    }

    public static bool IsCounted(AppDefinition app, SystemSnapshot s, CountMode mode) => mode switch
    {
        CountMode.All => s.RunningProcesses.Contains(app.Process),
        CountMode.ActiveOnly => s.ForegroundProcess != null && SameProcess(s.ForegroundProcess, app.Process)
            && !s.Windows.Any(w => w.Handle == s.ForegroundWindow && w.IsMinimized),
        _ => s.Windows.Any(w => !w.HasOwner && !w.IsMinimized && SameProcess(w.ProcessName, app.Process)),
    };

    private static IEnumerable<string> FileTitles(AppDefinition app, SystemSnapshot s, AppSettings settings)
    {
        var windows = s.Windows.Where(w => w.Title.Length > 0 && SameProcess(w.ProcessName, app.Process));
        if (!settings.IgnoreWindowStateForFiles)
        {
            windows = settings.CountMode switch
            {
                CountMode.All => windows,
                CountMode.ActiveOnly => windows.Where(w => w.Handle == s.ForegroundWindow),
                _ => windows.Where(w => !w.IsMinimized),
            };
        }
        return windows.Select(w => w.Title);
    }

    private static bool SameProcess(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    private static void Touch(Dictionary<string, ActiveSession> map, string key, AppDefinition app, string file, DateTime now, TimeSpan tolerance)
    {
        if (!map.TryGetValue(key, out var session))
        {
            session = new ActiveSession
            {
                SessionId = Guid.NewGuid().ToString("N")[..12],
                Process = app.Process,
                AppName = app.DisplayName,
                File = file,
                Start = now,
                LastSeen = now,
            };
            map[key] = session;
        }
        else
        {
            // 前回の計測から間が空きすぎていなければ、その間を作業時間として数える
            var delta = now - session.LastSeen;
            if (delta > TimeSpan.Zero && delta <= tolerance)
            {
                // 0時をまたいだ分は前日と当日に分ける
                var midnight = now.Date;
                if (session.LastSeen < midnight)
                {
                    AddSeconds(session, DateOnly.FromDateTime(session.LastSeen), (midnight - session.LastSeen).TotalSeconds);
                    AddSeconds(session, DateOnly.FromDateTime(now), (now - midnight).TotalSeconds);
                }
                else
                {
                    AddSeconds(session, DateOnly.FromDateTime(now), delta.TotalSeconds);
                }
            }
            session.LastSeen = now;
            session.AppName = app.DisplayName;
        }
        session.SeenInLastTick = true;
    }

    private static void AddSeconds(ActiveSession session, DateOnly day, double seconds)
    {
        if (seconds > 0) session.SecondsByDay[day] = session.SecondsByDay.GetValueOrDefault(day) + seconds;
    }

    private static void Expire(Dictionary<string, ActiveSession> map, HashSet<string> seen, DateTime now, TimeSpan grace,
        AppSettings settings, RecordKind kind, List<FinishedSession> finished)
    {
        foreach (var (key, session) in map.ToList())
        {
            if (seen.Contains(key)) continue;
            session.SeenInLastTick = false;
            if (now - session.LastSeen <= grace) continue;

            map.Remove(key);
            if (Finish(kind, session, null, settings.MinRecordSeconds) is { } f) finished.Add(f);
        }
    }

    /// <summary>作業を締めて保存用の行を作る。短すぎる作業は null。</summary>
    public static FinishedSession? Finish(RecordKind kind, ActiveSession session, DateTime? cutoff, int minRecordSeconds)
    {
        var end = session.LastSeen;
        var byDay = new SortedDictionary<DateOnly, double>(session.SecondsByDay);

        if (cutoff is { } c && c < end)
        {
            var newEnd = c < session.Start ? session.Start : c;
            var trim = (end - newEnd).TotalSeconds;
            foreach (var day in byDay.Keys.Reverse().ToList())
            {
                if (trim <= 0) break;
                var take = Math.Min(trim, byDay[day]);
                byDay[day] -= take;
                trim -= take;
            }
            end = newEnd;
        }

        var total = (long)Math.Round(byDay.Values.Sum());
        if (total < Math.Max(1, minRecordSeconds)) return null;

        // 日ごとに行を分ける。丸めた合計が total と一致するよう累積で丸める
        var records = new List<SessionRecord>();
        double cumulative = 0;
        long emitted = 0;
        foreach (var (day, seconds) in byDay)
        {
            cumulative += seconds;
            var upTo = (long)Math.Round(cumulative);
            var piece = upTo - emitted;
            emitted = upTo;
            if (piece <= 0) continue;

            var dayStart = day.ToDateTime(TimeOnly.MinValue);
            var dayEnd = dayStart.AddDays(1).AddSeconds(-1);
            var pieceStart = session.Start > dayStart ? session.Start : dayStart;
            var pieceEnd = end < dayEnd ? end : dayEnd;
            if (pieceEnd < pieceStart) pieceEnd = pieceStart;

            records.Add(new SessionRecord
            {
                Start = TruncateToSecond(pieceStart),
                End = TruncateToSecond(pieceEnd),
                Seconds = piece,
                Process = session.Process,
                AppName = session.AppName,
                File = session.File,
                SessionId = session.SessionId,
            });
        }

        return new FinishedSession(kind, session.SessionId, session.Process, session.AppName, session.File,
            TruncateToSecond(session.Start), TruncateToSecond(end), total, records);
    }

    private static DateTime TruncateToSecond(DateTime t) => new(t.Ticks - t.Ticks % TimeSpan.TicksPerSecond, t.Kind);
}
