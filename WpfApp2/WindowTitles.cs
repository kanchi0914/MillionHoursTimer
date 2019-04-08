using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

public static class WindowTitles
{
    [DllImport("user32")]
    private static extern bool EnumWindows(WNDENUMPROC lpEnumFunc, IntPtr lParam);

    // EnumWindowsから呼び出されるコールバック関数WNDENUMPROCのデリゲート
    private delegate bool WNDENUMPROC(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32", CharSet = CharSet.Auto)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32")]
    private static extern int GetWindowThreadProcessId(IntPtr hWnd, out int lpdwProcessId);

    public static string ProcessName = "";
    public static List<string> Titles = new List<string>();

    public static void Get(string processName)
    {
        ProcessName = processName;
        Titles = new List<string>();
        EnumWindows(EnumerateWindow, IntPtr.Zero);
    }

    // ウィンドウを列挙するためのコールバックメソッド
    private static bool EnumerateWindow(IntPtr hWnd, IntPtr lParam)
    {
        // ウィンドウが可視かどうか調べる
        if (IsWindowVisible(hWnd))
            // 可視の場合
            PrintCaptionAndProcess(hWnd);

        // ウィンドウの列挙を継続するにはtrueを返す必要がある
        return true;
    }

    // ウィンドウのキャプションとプロセス名を表示する
    public static void PrintCaptionAndProcess(IntPtr hWnd)
    {
        // ウィンドウのキャプションを取得・表示
        StringBuilder caption = new StringBuilder(0x1000);

        GetWindowText(hWnd, caption, caption.Capacity);

        // ウィンドウハンドルからプロセスIDを取得
        int processId;

        GetWindowThreadProcessId(hWnd, out processId);

        // プロセスIDからProcessクラスのインスタンスを取得
        Process p = Process.GetProcessById(processId);

        if (p.ProcessName == ProcessName && !string.IsNullOrEmpty(caption.ToString()))
        {
            Titles.Add(caption.ToString());
            //Console.Write("'{0}' ", caption);
            //Console.WriteLine("({0})", p.ProcessName);
        }
    }
}