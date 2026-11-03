using System.Diagnostics;
using MillionHoursTimer.Core.Tracking;

namespace MillionHoursTimer.Platform;

/// <summary>Windows API で起動中のプロセスとウィンドウを調べる</summary>
internal sealed class WindowsSystemProbe : ISystemProbe
{
    public SystemSnapshot Capture(IReadOnlySet<string> processes)
    {
        var now = DateTime.Now;

        // プロセスの一覧は1回だけ取り、ウィンドウのプロセス名もここから引く
        var running = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var namesById = new Dictionary<uint, string>();
        foreach (var p in Process.GetProcesses())
        {
            using (p)
            {
                running.Add(p.ProcessName);
                namesById[(uint)p.Id] = p.ProcessName;
            }
        }

        var windows = new List<WindowInfo>();
        var shell = Win32.GetShellWindow();
        Win32.EnumWindows((hWnd, _) =>
        {
            if (hWnd == shell || !Win32.IsWindowVisible(hWnd)) return true;
            Win32.GetWindowThreadProcessId(hWnd, out var pid);
            if (!namesById.TryGetValue(pid, out var name) || !processes.Contains(name)) return true;

            windows.Add(new WindowInfo(hWnd, Win32.GetTitle(hWnd), name, Win32.IsIconic(hWnd),
                Win32.GetWindow(hWnd, Win32.GW_OWNER) != 0));
            return true;
        }, 0);

        var foreground = Win32.GetForegroundWindow();
        string? foregroundProcess = null;
        if (foreground != 0)
        {
            Win32.GetWindowThreadProcessId(foreground, out var pid);
            foregroundProcess = namesById.GetValueOrDefault(pid);
        }

        return new SystemSnapshot(now, running, windows, foreground, foregroundProcess, Win32.GetIdleSeconds());
    }
}
