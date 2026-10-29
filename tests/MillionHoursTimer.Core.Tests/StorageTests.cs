using System.Text;
using MillionHoursTimer.Core.Csv;
using MillionHoursTimer.Core.Model;
using MillionHoursTimer.Core.Storage;

namespace MillionHoursTimer.Core.Tests;

public class RecordStoreTests : IDisposable
{
    private readonly TempDir _dir = new();
    private readonly RecordStore _store;

    public RecordStoreTests()
    {
        _store = new RecordStore(_dir.Combine("apps"), _dir.Combine("files"));
    }

    public void Dispose() => _dir.Dispose();

    private static SessionRecord Rec(DateTime start, long seconds, string process = "clip", string file = "") => new()
    {
        Start = start,
        End = start.AddSeconds(seconds),
        Seconds = seconds,
        Process = process,
        AppName = "クリスタ, 本体",
        File = file,
        SessionId = "s1",
    };

    [Fact]
    public void AppendsToMonthlyFilesWithHeaderAndBom()
    {
        _store.Append(RecordKind.App, [Rec(new DateTime(2026, 10, 1, 10, 0, 0), 60), Rec(new DateTime(2026, 11, 2, 10, 0, 0), 30)]);
        _store.Append(RecordKind.App, [Rec(new DateTime(2026, 10, 3, 10, 0, 0), 15)]);

        var october = _dir.Combine("apps", "2026-10.csv");
        var bytes = File.ReadAllBytes(october);
        Assert.Equal([0xEF, 0xBB, 0xBF], bytes[..3]);
        Assert.Equal(
            "start,end,seconds,process,app_name,session_id\r\n" +
            "2026-10-01 10:00:00,2026-10-01 10:01:00,60,clip,\"クリスタ, 本体\",s1\r\n" +
            "2026-10-03 10:00:00,2026-10-03 10:00:15,15,clip,\"クリスタ, 本体\",s1\r\n",
            TextFile.Read(october).Text);
        Assert.True(File.Exists(_dir.Combine("apps", "2026-11.csv")));
        Assert.Equal(105, _store.LoadAll(RecordKind.App).Sum(r => r.Seconds));
    }

    [Fact]
    public void LegacyRecordsWithoutStartGoToLegacyFile()
    {
        _store.Append(RecordKind.File, [new SessionRecord { Seconds = 3600, Process = "sai", File = "a.sai", SessionId = "legacy" }]);
        var loaded = Assert.Single(_store.LoadAll(RecordKind.File));
        Assert.Null(loaded.Start);
        Assert.Equal(3600, loaded.Seconds);
        Assert.True(File.Exists(_dir.Combine("files", "legacy.csv")));
    }

    [Fact]
    public void NormalizesFileSavedAsShiftJisWithReorderedColumns()
    {
        // Excel で列を入れ替えて Shift-JIS で保存され、最後の改行もない
        var path = _dir.Combine("apps", "2026-10.csv");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, Encoding.GetEncoding(932).GetBytes(
            "process,app_name,seconds,start,end,session_id,memo\r\nclip,クリスタ,100,2026/10/01 9:00,2026/10/01 9:01,x,メモ"));

        _store.Append(RecordKind.App, [Rec(new DateTime(2026, 10, 2, 10, 0, 0), 20)]);

        var (text, isUtf8) = TextFile.Read(path);
        Assert.True(isUtf8);
        Assert.StartsWith("start,end,seconds,process,app_name,session_id\r\n2026-10-01 09:00:00,2026-10-01 09:01:00,100,clip,クリスタ,x\r\n", text);
        Assert.Equal([100L, 20L], _store.LoadAll(RecordKind.App).Select(r => r.Seconds));
    }

    [Fact]
    public void AppendsNewlineWhenFileDoesNotEndWithOne()
    {
        var path = _dir.Combine("apps", "2026-10.csv");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "start,end,seconds,process,app_name,session_id\r\n2026-10-01 09:00:00,,100,clip,,x", TextFile.Utf8Bom);

        _store.Append(RecordKind.App, [Rec(new DateTime(2026, 10, 2, 10, 0, 0), 20)]);

        Assert.Equal(2, _store.LoadAll(RecordKind.App).Count);
    }

    [Fact]
    public void SecondsCanBeOmittedWhenStartAndEndExist()
    {
        var path = _dir.Combine("apps", "2026-10.csv");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "start,end,seconds,process\r\n2026-10-01 09:00:00,2026-10-01 10:30:00,,clip\r\n");

        Assert.Equal(5400, Assert.Single(_store.LoadAll(RecordKind.App)).Seconds);
    }

    [Fact]
    public void RewriteDeletesAndRenamesButKeepsUnreadableRows()
    {
        var path = _dir.Combine("files", "2026-10.csv");
        _store.Append(RecordKind.File, [Rec(new DateTime(2026, 10, 1, 9, 0, 0), 10, file: "a.png"), Rec(new DateTime(2026, 10, 1, 9, 0, 0), 20, file: "b.png")]);
        File.AppendAllText(path, "壊れた行,,,,,,\r\n");

        var warnings = new List<string>();
        _store.LoadAll(RecordKind.File, warnings);
        Assert.Single(warnings);

        var changed = _store.Rewrite(RecordKind.File, r => r.File == "a.png" ? null : r with { File = "c.png" });

        Assert.Equal(2, changed);
        Assert.Equal("c.png", Assert.Single(_store.LoadAll(RecordKind.File)).File);
        Assert.Contains("壊れた行", TextFile.Read(path).Text);
    }

    [Fact]
    public void AppendFailsWhileFileIsLockedAndSucceedsAfterwards()
    {
        _store.Append(RecordKind.App, [Rec(new DateTime(2026, 10, 1, 9, 0, 0), 10)]);
        var path = _dir.Combine("apps", "2026-10.csv");

        using (new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            Assert.ThrowsAny<IOException>(() => _store.Append(RecordKind.App, [Rec(new DateTime(2026, 10, 2, 9, 0, 0), 5)]));
        }
        _store.Append(RecordKind.App, [Rec(new DateTime(2026, 10, 2, 9, 0, 0), 5)]);
        Assert.Equal(2, _store.LoadAll(RecordKind.App).Count);
    }
}

public class AppListStoreTests
{
    [Fact]
    public void RoundTripsApps()
    {
        using var dir = new TempDir();
        var path = dir.Combine("apps.csv");
        AppListStore.Save(path,
        [
            new AppDefinition { Process = "CLIPStudioPaint", Name = "クリスタ, 漫画", ExePath = @"C:\Program Files\CELSYS\CLIPStudioPaint.exe",
                Extensions = [".clip", ".png"], TogglEnabled = true, TogglProject = "絵", TogglTag = "illust" },
            new AppDefinition { Process = "Code" },
        ]);

        var apps = AppListStore.Load(path);

        Assert.Equal(2, apps.Count);
        Assert.Equal("クリスタ, 漫画", apps[0].Name);
        Assert.Equal([".clip", ".png"], apps[0].Extensions);
        Assert.True(apps[0].TogglEnabled);
        Assert.Equal("illust", apps[0].TogglTag);
        Assert.Equal("Code", apps[1].DisplayName);
    }

    [Fact]
    public void SkipsDuplicatesAndEmptyProcess()
    {
        using var dir = new TempDir();
        var path = dir.Combine("apps.csv");
        File.WriteAllText(path, "process,app_name\r\ncode,A\r\n,B\r\nCODE,C\r\n");
        var warnings = new List<string>();

        var apps = AppListStore.Load(path, warnings);

        Assert.Equal("A", Assert.Single(apps).Name);
        Assert.Equal(2, warnings.Count);
    }
}

public class SettingsStoreTests
{
    [Fact]
    public void RoundTripsAndBacksUpBrokenFile()
    {
        using var dir = new TempDir();
        var path = dir.Combine("settings.json");
        SettingsStore.Save(path, new AppSettings { CountMode = CountMode.ActiveOnly, IdleTimeoutSeconds = 900 });

        var json = File.ReadAllText(path);
        Assert.Contains("\"countMode\": \"ActiveOnly\"", json);
        Assert.Equal(900, SettingsStore.Load(path).IdleTimeoutSeconds);

        File.WriteAllText(path, "{ broken");
        var warnings = new List<string>();
        var loaded = SettingsStore.Load(path, warnings);
        Assert.Equal(CountMode.NotMinimized, loaded.CountMode);
        Assert.Single(warnings);
        Assert.Single(Directory.GetFiles(dir.Path, "settings.json.broken-*"));
    }

    [Fact]
    public void AcceptsCommentsAndTrailingCommasFromHandEditing()
    {
        using var dir = new TempDir();
        var path = dir.Combine("settings.json");
        File.WriteAllText(path, "{\n  // 15分\n  \"idleTimeoutSeconds\": 900,\n}");
        Assert.Equal(900, SettingsStore.Load(path).IdleTimeoutSeconds);
    }
}
