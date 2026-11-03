using System.Runtime.CompilerServices;
using System.Text;
using MillionHoursTimer.Core.Tracking;

namespace MillionHoursTimer.Core.Tests;

internal static class TestSetup
{
    [ModuleInitializer]
    internal static void Init() => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
}

public sealed class TempDir : IDisposable
{
    public TempDir()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "mht-test-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public string Combine(params string[] parts) => System.IO.Path.Combine([Path, .. parts]);

    public void Dispose()
    {
        try
        {
            Directory.Delete(Path, recursive: true);
        }
        catch (IOException)
        {
        }
    }
}

/// <summary>計測対象の PC の状態を作るためのヘルパー</summary>
public static class Snap
{
    private static nint _nextHandle = 100;

    public static WindowInfo Window(string process, string title = "", bool minimized = false, bool owned = false) =>
        new(_nextHandle++, title, process, minimized, owned);

    public static SystemSnapshot At(DateTime now, params WindowInfo[] windows) => At(now, 0, null, windows);

    public static SystemSnapshot At(DateTime now, int idleSeconds, WindowInfo? foreground, params WindowInfo[] windows)
    {
        var all = foreground != null && !windows.Contains(foreground) ? [.. windows, foreground] : windows;
        var running = all.Select(w => w.ProcessName).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return new SystemSnapshot(now, running, all, foreground?.Handle ?? 0, foreground?.ProcessName, idleSeconds);
    }
}

public sealed class FakeProbe : ISystemProbe
{
    public Func<SystemSnapshot> Next { get; set; } = () => Snap.At(DateTime.Now);

    public SystemSnapshot Capture(IReadOnlySet<string> processes) => Next();
}
