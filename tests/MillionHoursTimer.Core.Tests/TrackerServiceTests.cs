using MillionHoursTimer.Core.Legacy;
using MillionHoursTimer.Core.Model;
using MillionHoursTimer.Core.Storage;

namespace MillionHoursTimer.Core.Tests;

public class TrackerServiceTests : IDisposable
{
    private readonly TempDir _dir = new();
    private readonly DataPaths _paths;
    private readonly FakeProbe _probe = new();
    private DateTime _now = DateTime.Today.AddHours(10);

    public TrackerServiceTests()
    {
        _paths = new DataPaths(_dir.Path);
    }

    public void Dispose() => _dir.Dispose();

    private TrackerService Create() => new(_paths, _probe, NullLog.Instance, null);

    private void TickFor(TrackerService service, int seconds, params Tracking.WindowInfo[] windows)
    {
        for (var i = 0; i < seconds; i++)
        {
            _now = _now.AddSeconds(1);
            var now = _now;
            _probe.Next = () => Snap.At(now, windows);
            service.Tick();
        }
    }

    private RecordStore Records => new(_paths);

    [Fact]
    public void RecordsSessionsOnShutdownAndShowsTotals()
    {
        var service = Create();
        Assert.True(service.IsFirstRun);
        service.AddApp("clip", "クリスタ", @"C:\clip.exe");

        TickFor(service, 11, Snap.Window("clip", "a.clip - CLIP"));
        var status = Assert.Single(service.GetSnapshot().Apps);
        Assert.True(status.IsRecording);
        Assert.Equal(10, status.TodaySeconds);
        service.Shutdown();

        Assert.Equal(10, Assert.Single(Records.LoadAll(RecordKind.App)).Seconds);
        Assert.Equal("a.clip", Assert.Single(Records.LoadAll(RecordKind.File)).File);
        Assert.False(File.Exists(_paths.CheckpointFile));

        // 再起動後も合計が読める
        var restarted = Create();
        Assert.False(restarted.IsFirstRun);
        Assert.Equal(10, Assert.Single(restarted.GetSnapshot().Apps).TotalSeconds);
        Assert.Equal(10, Assert.Single(restarted.GetFiles("clip")).TotalSeconds);
    }

    [Fact]
    public void RecoversSessionsAfterCrash()
    {
        var service = Create();
        service.AddApp("clip", "クリスタ", "");
        TickFor(service, 21, Snap.Window("clip"));
        // Shutdown を呼ばずに落ちた想定
        Assert.True(File.Exists(_paths.CheckpointFile));

        var restarted = Create();

        Assert.Equal(20, Assert.Single(Records.LoadAll(RecordKind.App)).Seconds);
        Assert.Equal(20, Assert.Single(restarted.GetSnapshot().Apps).TotalSeconds);

        // 復旧した分が二重に記録されないこと
        Create().Shutdown();
        Assert.Single(Records.LoadAll(RecordKind.App));
    }

    [Fact]
    public void KeepsRecordsInMemoryWhileFileIsLocked()
    {
        var service = Create();
        service.AddApp("clip", "", "");
        TickFor(service, 6, Snap.Window("clip"));
        service.Suspend();   // 1件目を書く
        var path = Records.PathOf(RecordKind.App, _now.ToString("yyyy-MM"));
        Assert.True(File.Exists(path));

        using (new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            TickFor(service, 6, Snap.Window("clip"));
            service.Suspend();
            Assert.Equal(1, service.GetSnapshot().UnsavedCount);
            Assert.Equal(10, service.GetSnapshot().Apps[0].TodaySeconds);
        }

        TickFor(service, 1);
        Assert.Equal(0, service.GetSnapshot().UnsavedCount);
        Assert.Equal(2, Records.LoadAll(RecordKind.App).Count);
    }

    [Fact]
    public void ReloadPicksUpHandEditedRecords()
    {
        var service = Create();
        service.AddApp("clip", "", "");
        Directory.CreateDirectory(_paths.AppRecordsDir);
        File.WriteAllText(Path.Combine(_paths.AppRecordsDir, "manual.csv"), "start,seconds,process\r\n2026-01-01 10:00:00,3600,clip\r\n");

        service.ReloadFromDisk();

        Assert.Equal(3600, service.GetSnapshot().Apps[0].TotalSeconds);
    }

    [Fact]
    public void ReloadKeepsTotalsWhenARecordFileIsLocked()
    {
        var service = Create();
        service.AddApp("clip", "", "");
        var path = Path.Combine(_paths.AppRecordsDir, "manual.csv");
        File.WriteAllText(path, "start,seconds,process\r\n2026-01-01 10:00:00,3600,clip\r\n");
        service.ReloadFromDisk();

        using (new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            service.ReloadFromDisk();
            Assert.Equal(3600, service.GetSnapshot().Apps[0].TotalSeconds);
        }
    }

    [Fact]
    public void MergeAndDeleteFiles()
    {
        var service = Create();
        service.AddApp("code", "", "");
        service.UpdateApp("code", a => a.Extensions = [".cs"]);
        TickFor(service, 6, Snap.Window("code", "A.cs - code"));
        TickFor(service, 62, Snap.Window("code", "B.cs - code"));   // A.cs は猶予時間を過ぎて記録される
        TickFor(service, 4, Snap.Window("code", "C.cs - code"));

        service.MergeFiles("code", ["A.cs", "B.cs"], "AB.cs");
        var files = service.GetFiles("code").ToDictionary(f => f.Name, f => f.TotalSeconds);
        Assert.Equal(5 + 61, files["AB.cs"]);
        Assert.True(files.ContainsKey("C.cs"));

        service.DeleteFiles("code", ["AB.cs"]);
        service.Shutdown();
        Assert.DoesNotContain(Records.LoadAll(RecordKind.File), r => r.File == "AB.cs");
        Assert.Equal("C.cs", Assert.Single(Records.LoadAll(RecordKind.File)).File);
        // アプリ単位の記録は消えない
        Assert.Equal(5 + 62 + 4, Records.LoadAll(RecordKind.App).Sum(r => r.Seconds));
    }

    private void SeedFile(string process, string file, int daysAgo, long seconds)
    {
        var start = DateTime.Today.AddDays(-daysAgo).AddHours(9);
        Records.Append(RecordKind.File,
        [
            new SessionRecord { Start = start, End = start.AddSeconds(seconds), Seconds = seconds, Process = process, AppName = process, File = file, SessionId = file },
        ]);
    }

    [Fact]
    public void DeletesExpiredFilesExceptLockedLongAndUndated()
    {
        SeedFile("chrome", "old-short", 40, 60);
        SeedFile("chrome", "old-long", 40, 600);
        SeedFile("chrome", "old-locked", 40, 60);
        SeedFile("chrome", "recent", 5, 60);
        Records.Append(RecordKind.File, [new SessionRecord { Seconds = 60, Process = "chrome", File = "legacy", SessionId = "legacy" }]);
        SeedFile("other", "old-short", 40, 60);

        var service = Create();
        service.AddApp("chrome", "", "");
        service.AddApp("other", "", "");
        service.SetFilesLocked("chrome", ["old-locked"], true);
        service.UpdateApp("chrome", a =>
        {
            a.FileExpireDays = 30;
            a.FileKeepMinutes = 10;
        });

        // 起動して最初の計測で消える
        TickFor(service, 1);

        Assert.Equal(["legacy", "old-locked", "old-long", "recent"], service.GetFiles("chrome").Select(f => f.Name).Order());
        Assert.True(service.GetFiles("chrome").Single(f => f.Name == "old-locked").IsLocked);
        // 自動削除を設定していないアプリは消えない
        Assert.Single(service.GetFiles("other"));
        Assert.DoesNotContain(Records.LoadAll(RecordKind.File), r => r.Process == "chrome" && r.File == "old-short");
    }

    [Fact]
    public void SetFileCleanupDeletesRightAway()
    {
        SeedFile("chrome", "a", 10, 60);
        SeedFile("chrome", "b", 3, 60);
        var service = Create();
        service.AddApp("chrome", "", "");
        TickFor(service, 1);

        Assert.Equal(1, service.CountExpiredFiles("chrome", 7, 0));
        Assert.Equal(2, service.CountExpiredFiles("chrome", 1, 0));
        Assert.Equal(1, service.SetFileCleanup("chrome", 7, 0));

        Assert.Equal("b", Assert.Single(service.GetFiles("chrome")).Name);
        Assert.Equal(7, Assert.Single(AppListStore.Load(_paths.AppsFile)).FileExpireDays);
    }

    [Fact]
    public void LocksSurviveRestartAndFollowMergeAndDelete()
    {
        SeedFile("code", "A.cs", 1, 10);
        SeedFile("code", "B.cs", 1, 10);
        SeedFile("code", "C.cs", 1, 10);
        var service = Create();
        service.AddApp("code", "", "");
        service.SetFilesLocked("code", ["A.cs"], true);

        var restarted = Create();
        Assert.Equal(["A.cs"], restarted.GetFiles("code").Where(f => f.IsLocked).Select(f => f.Name));

        // まとめた後もロックは残る
        restarted.MergeFiles("code", ["A.cs", "B.cs"], "AB.cs");
        Assert.Equal(["AB.cs"], restarted.GetFiles("code").Where(f => f.IsLocked).Select(f => f.Name));

        // 削除したらロックも外れる (同じ名前で記録し直してもロックされていない)
        restarted.DeleteFiles("code", ["AB.cs"]);
        Assert.Empty(FileLockStore.Load(_paths.FileLocksFile));

        restarted.SetFilesLocked("code", ["C.cs"], true);
        restarted.SetFilesLocked("code", ["C.cs"], false);
        Assert.All(restarted.GetFiles("code"), f => Assert.False(f.IsLocked));
    }

    [Fact]
    public void ShortSessionsAreNotRecordedPerFile()
    {
        var service = Create();
        service.AddApp("chrome", "", "");
        var settings = service.Settings;
        settings.MinFileRecordSeconds = 30;
        service.UpdateSettings(settings);

        TickFor(service, 11, Snap.Window("chrome", "検索 - Google Chrome"));
        service.Shutdown();

        Assert.Equal(10, Assert.Single(Records.LoadAll(RecordKind.App)).Seconds);
        Assert.Empty(Records.LoadAll(RecordKind.File));
    }

    [Fact]
    public void RemoveAppOptionallyKeepsRecords()
    {
        var service = Create();
        service.AddApp("a", "", "");
        service.AddApp("b", "", "");
        TickFor(service, 6, Snap.Window("a"), Snap.Window("b"));

        service.RemoveApp("a", deleteRecords: false);
        service.RemoveApp("b", deleteRecords: true);
        service.Shutdown();

        Assert.Equal("a", Assert.Single(Records.LoadAll(RecordKind.App)).Process);
        Assert.Empty(AppListStore.Load(_paths.AppsFile));
        Assert.Contains(service.GetHistory(DateOnly.MinValue, DateOnly.MaxValue), h => h.Process == "a");
    }

    [Fact]
    public void ImportsLegacyTotalsWithoutTouchingSource()
    {
        var oldDir = Path.Combine(Path.GetTempPath(), "mht-old-" + Guid.NewGuid().ToString("N")[..8], "data");
        Directory.CreateDirectory(Path.Combine(oldDir, "fileData"));
        File.WriteAllText(Path.Combine(oldDir, "appData.csv"), "dummy");
        File.WriteAllText(Path.Combine(oldDir, "fileData", "clip_files.csv"), "dummy");
        var legacyData = new LegacyData(oldDir,
        [
            new LegacyApp("clip", 7200, true, "絵", "", [".clip"], null, [new LegacyFile("a.clip", 3600)]),
            new LegacyApp("devenv", 0, false, "", "", [], null, []),
        ], DateTime.Now, []);
        var legacySettings = new LegacySettings(new Dictionary<string, string> { ["NoInputTime"] = "900" });

        var service = Create();
        var result = service.ImportLegacy(legacyData, legacySettings);

        Assert.Equal((2, 7200L, 1), (result.AppsAdded, result.Seconds, result.Files));
        Assert.True(File.Exists(Path.Combine(result.BackupDir, "data", "fileData", "clip_files.csv")));
        Assert.True(File.Exists(Path.Combine(oldDir, "appData.csv")));
        var apps = service.GetSnapshot().Apps;
        Assert.Equal(7200, apps.Single(a => a.Process == "clip").TotalSeconds);
        Assert.Equal(0, apps.Single(a => a.Process == "clip").TodaySeconds);
        Assert.True(apps.Single(a => a.Process == "clip").TogglEnabled);
        Assert.Equal(900, service.Settings.IdleTimeoutSeconds);
        Assert.Equal(oldDir, service.Settings.Migration?.SourceDir);
        Assert.True(File.Exists(Path.Combine(_paths.AppRecordsDir, "legacy.csv")));
        Assert.Equal(3600, service.GetFiles("clip").Single().TotalSeconds);
    }

    [Fact]
    public void HistoryIncludesActiveSessions()
    {
        var service = Create();
        service.AddApp("clip", "クリスタ", "");
        TickFor(service, 11, Snap.Window("clip"));

        var entry = Assert.Single(service.GetHistory(DateOnly.FromDateTime(_now), DateOnly.FromDateTime(_now)));

        Assert.Equal("クリスタ", entry.Name);
        Assert.Equal(10, entry.Total);
    }
}
