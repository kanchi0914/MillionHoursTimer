using MillionHoursTimer.Core.Model;
using MillionHoursTimer.Core.Tracking;

namespace MillionHoursTimer.Core.Tests;

public class TrackingEngineTests
{
    private static readonly DateTime T0 = new(2026, 10, 29, 21, 0, 0);

    private static AppDefinition App(string process, params string[] extensions) =>
        new() { Process = process, Name = process + " name", Extensions = [.. extensions] };

    private static AppSettings Settings(CountMode mode = CountMode.NotMinimized) =>
        new() { CountMode = mode, IdleTimeoutSeconds = 60, UseHyphenFileName = false };

    /// <summary>start から seconds 秒間、毎秒 snapshot を渡す</summary>
    private static List<FinishedSession> Run(TrackingEngine engine, DateTime start, int seconds,
        Func<DateTime, SystemSnapshot> snapshot, IReadOnlyList<AppDefinition> apps, AppSettings settings)
    {
        var finished = new List<FinishedSession>();
        for (var i = 0; i <= seconds; i++) finished.AddRange(engine.Tick(snapshot(start.AddSeconds(i)), apps, settings));
        return finished;
    }

    [Fact]
    public void CountsVisibleWindowAndEndsAfterGracePeriod()
    {
        var engine = new TrackingEngine();
        var apps = new[] { App("clip") };
        var settings = Settings();
        var window = Snap.Window("clip", "work");

        Assert.Empty(Run(engine, T0, 10, t => Snap.At(t, window), apps, settings));
        Assert.True(engine.IsRecording("clip"));

        // 最小化したら記録中ではなくなり、猶予時間が過ぎたら終了する
        var minimized = window with { IsMinimized = true };
        var finished = Run(engine, T0.AddSeconds(11), 61, t => Snap.At(t, minimized), apps, settings);
        Assert.False(engine.IsRecording("clip"));

        var f = Assert.Single(finished);
        Assert.Equal(RecordKind.App, f.Kind);
        Assert.Equal(10, f.Seconds);
        Assert.Equal(T0, f.Start);
        Assert.Equal(T0.AddSeconds(10), f.End);
        var r = Assert.Single(f.Records);
        Assert.Equal("clip name", r.AppName);
        Assert.Equal(10, r.Seconds);
    }

    [Fact]
    public void ResumingWithinGraceContinuesTheSameSessionWithoutCountingTheGap()
    {
        var engine = new TrackingEngine();
        var apps = new[] { App("clip") };
        var settings = Settings();
        var window = Snap.Window("clip");

        Run(engine, T0, 10, t => Snap.At(t, window), apps, settings);
        Run(engine, T0.AddSeconds(11), 19, t => Snap.At(t), apps, settings);   // 20秒間いなくなる
        Run(engine, T0.AddSeconds(31), 9, t => Snap.At(t, window), apps, settings);

        var f = Assert.Single(engine.EndAll(null, settings));
        Assert.Equal(T0, f.Start);
        Assert.Equal(10 + 9, f.Seconds);
    }

    [Fact]
    public void NotMinimizedModeIgnoresOwnedToolWindows()
    {
        var engine = new TrackingEngine();
        var snapshot = Snap.At(T0, Snap.Window("clip", minimized: true), Snap.Window("clip", "palette", owned: true));
        engine.Tick(snapshot, [App("clip")], Settings());
        Assert.False(engine.IsRecording("clip"));
    }

    [Fact]
    public void AllModeCountsRunningProcessWithoutWindows()
    {
        var engine = new TrackingEngine();
        var snapshot = new SystemSnapshot(T0, new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Dropbox" }, [], 0, null, 0);
        engine.Tick(snapshot, [App("dropbox")], Settings(CountMode.All));
        Assert.True(engine.IsRecording("dropbox"));
    }

    [Fact]
    public void ActiveOnlyModeCountsOnlyForegroundApp()
    {
        var engine = new TrackingEngine();
        var apps = new[] { App("clip"), App("code") };
        var clip = Snap.Window("clip");
        var code = Snap.Window("code");

        engine.Tick(Snap.At(T0, 0, code, clip, code), apps, Settings(CountMode.ActiveOnly));

        Assert.True(engine.IsRecording("code"));
        Assert.False(engine.IsRecording("clip"));
    }

    [Fact]
    public void IdleEndsSessionsAtLastInputTime()
    {
        var engine = new TrackingEngine();
        var apps = new[] { App("clip") };
        var settings = Settings();
        var window = Snap.Window("clip");

        // 100秒作業したあと、60秒間操作がなかった
        Run(engine, T0, 159, t => Snap.At(t, Math.Max(0, (int)(t - T0).TotalSeconds - 100), null, window), apps, settings);
        var finished = engine.Tick(Snap.At(T0.AddSeconds(160), 60, null, window), apps, settings);

        Assert.True(engine.IsIdle);
        var f = Assert.Single(finished);
        Assert.Equal(100, f.Seconds);
        Assert.Equal(T0.AddSeconds(100), f.End);
    }

    [Fact]
    public void SplitsSessionsAtMidnight()
    {
        var engine = new TrackingEngine();
        var apps = new[] { App("clip") };
        var settings = Settings();
        var window = Snap.Window("clip");
        var start = new DateTime(2026, 10, 31, 23, 59, 0);

        Run(engine, start, 120, t => Snap.At(t, window), apps, settings);
        var f = Assert.Single(engine.EndAll(null, settings));

        Assert.Equal(120, f.Seconds);
        Assert.Equal(2, f.Records.Count);
        Assert.Equal((start, new DateTime(2026, 10, 31, 23, 59, 59), 60L), (f.Records[0].Start, f.Records[0].End, f.Records[0].Seconds));
        Assert.Equal((new DateTime(2026, 11, 1), start.AddSeconds(120), 60L), (f.Records[1].Start, f.Records[1].End, f.Records[1].Seconds));
        Assert.All(f.Records, r => Assert.Equal(f.SessionId, r.SessionId));
    }

    [Fact]
    public void DiscardsSessionsShorterThanMinimum()
    {
        var engine = new TrackingEngine();
        var settings = Settings();
        settings.MinRecordSeconds = 30;
        var window = Snap.Window("clip");

        Run(engine, T0, 20, t => Snap.At(t, window), [App("clip")], settings);

        Assert.Empty(engine.EndAll(null, settings));
    }

    [Fact]
    public void TracksFilesFromWindowTitles()
    {
        var engine = new TrackingEngine();
        var apps = new[] { App("Photoshop", ".psd", ".png") };
        var settings = Settings();
        var a = Snap.Window("Photoshop", "a.psd @ 50%");
        var b = Snap.Window("Photoshop", "b.png @ 100%", minimized: true);

        Run(engine, T0, 5, t => Snap.At(t, a, b), apps, settings);
        var finished = engine.EndAll(null, settings);

        var files = finished.Where(f => f.Kind == RecordKind.File).ToList();
        var file = Assert.Single(files);   // 最小化しているウィンドウのファイルは数えない
        Assert.Equal("a.psd", file.File);
        Assert.Equal(5, file.Seconds);
    }

    [Fact]
    public void IgnoreWindowStateCountsFilesInMinimizedWindowsWhileAppIsCounted()
    {
        var engine = new TrackingEngine();
        var apps = new[] { App("CLIPStudioPaint", ".clip") };
        var settings = Settings();
        settings.IgnoreWindowStateForFiles = true;
        var main = Snap.Window("CLIPStudioPaint", "CLIP STUDIO PAINT");
        var pages = Snap.Window("CLIPStudioPaint", "ページ管理 作品.clip", minimized: true, owned: true);

        Run(engine, T0, 3, t => Snap.At(t, main, pages), apps, settings);

        Assert.Contains(engine.FileSessions, s => s.File == "作品.clip");
    }

    [Fact]
    public void EndFilesFinishesOnlyThoseFiles()
    {
        var engine = new TrackingEngine();
        var apps = new[] { App("code", ".cs") };
        var settings = Settings();
        var w = Snap.Window("code", "A.cs - code");

        Run(engine, T0, 5, t => Snap.At(t, w), apps, settings);
        var ended = engine.EndFiles("code", new HashSet<string> { "A.cs" }, settings);

        Assert.Equal("A.cs", Assert.Single(ended).File);
        Assert.True(engine.IsRecording("code"));
        Assert.Empty(engine.FileSessions);
    }
}
