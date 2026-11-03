using System.Runtime.InteropServices;
using System.Text;

namespace MillionHoursTimer.Platform;

internal static class Win32
{
    public delegate bool EnumWindowsProc(nint hWnd, nint lParam);

    public const uint GW_OWNER = 4;

    [DllImport("user32.dll")]
    public static extern bool EnumWindows(EnumWindowsProc callback, nint lParam);

    [DllImport("user32.dll")]
    public static extern bool IsWindowVisible(nint hWnd);

    [DllImport("user32.dll")]
    public static extern bool IsIconic(nint hWnd);

    [DllImport("user32.dll")]
    public static extern nint GetWindow(nint hWnd, uint cmd);

    [DllImport("user32.dll")]
    public static extern nint GetShellWindow();

    [DllImport("user32.dll")]
    public static extern nint GetForegroundWindow();

    [DllImport("user32.dll")]
    public static extern uint GetWindowThreadProcessId(nint hWnd, out uint processId);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(nint hWnd, StringBuilder text, int maxCount);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowTextLength(nint hWnd);

    [StructLayout(LayoutKind.Sequential)]
    private struct LASTINPUTINFO
    {
        public uint cbSize;
        public uint dwTime;
    }

    [DllImport("user32.dll")]
    private static extern bool GetLastInputInfo(ref LASTINPUTINFO info);

    public static string GetTitle(nint hWnd)
    {
        var length = GetWindowTextLength(hWnd);
        if (length <= 0) return "";
        var sb = new StringBuilder(length + 1);
        GetWindowText(hWnd, sb, sb.Capacity);
        return sb.ToString();
    }

    /// <summary>最後にキーボード・マウスの操作があってからの秒数</summary>
    public static int GetIdleSeconds()
    {
        var info = new LASTINPUTINFO { cbSize = (uint)Marshal.SizeOf<LASTINPUTINFO>() };
        if (!GetLastInputInfo(ref info)) return 0;
        // TickCount は約49日で一周するので、符号なしの差で計算する
        var elapsed = unchecked((uint)Environment.TickCount - info.dwTime);
        return (int)(elapsed / 1000);
    }
}
