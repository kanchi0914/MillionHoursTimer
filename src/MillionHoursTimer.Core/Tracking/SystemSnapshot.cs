namespace MillionHoursTimer.Core.Tracking;

/// <summary>トップレベルウィンドウ1つ分の情報</summary>
/// <param name="HasOwner">ツールウィンドウなど、他のウィンドウに所有されているか</param>
public sealed record WindowInfo(nint Handle, string Title, string ProcessName, bool IsMinimized, bool HasOwner);

/// <summary>計測1回分の PC の状態</summary>
public sealed record SystemSnapshot(
    DateTime Now,
    IReadOnlySet<string> RunningProcesses,
    IReadOnlyList<WindowInfo> Windows,
    nint ForegroundWindow,
    string? ForegroundProcess,
    int IdleSeconds);

/// <summary>PC の状態を取得する (Windows API を使う実装はアプリ側にある)</summary>
public interface ISystemProbe
{
    /// <param name="processes">関心のあるプロセス名。これ以外のウィンドウは返さなくてよい</param>
    SystemSnapshot Capture(IReadOnlySet<string> processes);
}
