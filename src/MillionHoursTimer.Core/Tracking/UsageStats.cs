using MillionHoursTimer.Core.Model;

namespace MillionHoursTimer.Core.Tracking;

public sealed class FileUsage
{
    public required string Name { get; init; }
    public long TotalSeconds { get; set; }
    public DateOnly? LastDay { get; set; }
}

public sealed class AppUsage
{
    public long TotalSeconds { get; set; }

    /// <summary>記録に残っている最新の表示名 (一覧から削除したアプリの履歴表示に使う)</summary>
    public string LastAppName { get; set; } = "";

    public Dictionary<DateOnly, long> ByDay { get; } = [];
    public Dictionary<string, FileUsage> Files { get; } = new(StringComparer.Ordinal);

    public long SecondsOn(DateOnly day) => ByDay.GetValueOrDefault(day);
}

/// <summary>保存済みの記録から計算した集計 (計測中の作業は含まない)</summary>
public sealed class UsageStats
{
    private readonly Dictionary<string, AppUsage> _apps = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, AppUsage> Apps => _apps;

    public AppUsage? Get(string process) => _apps.GetValueOrDefault(process);

    public void Add(RecordKind kind, SessionRecord r)
    {
        if (!_apps.TryGetValue(r.Process, out var app))
        {
            app = new AppUsage();
            _apps[r.Process] = app;
        }
        if (r.AppName.Length > 0 && (r.Start != null || app.LastAppName.Length == 0)) app.LastAppName = r.AppName;

        if (kind == RecordKind.App)
        {
            app.TotalSeconds += r.Seconds;
            if (r.Day is { } day) app.ByDay[day] = app.ByDay.GetValueOrDefault(day) + r.Seconds;
            return;
        }

        if (!app.Files.TryGetValue(r.File, out var file))
        {
            file = new FileUsage { Name = r.File };
            app.Files[r.File] = file;
        }
        file.TotalSeconds += r.Seconds;
        if (r.Day is { } d && (file.LastDay is null || d > file.LastDay)) file.LastDay = d;
    }

    public void AddRange(RecordKind kind, IEnumerable<SessionRecord> records)
    {
        foreach (var r in records) Add(kind, r);
    }
}
