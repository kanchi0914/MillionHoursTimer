using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace WpfApp2
{
    public class WindowTitles2
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

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool IsIconic(IntPtr hWnd);

        public string ProcessName = "";
        public List<string> Titles = new List<string>();

        public List<string> Get(string processName)
        {
            ProcessName = processName;
            EnumWindows(EnumerateWindow, IntPtr.Zero);
            return Titles;
        }

        // ウィンドウを列挙するためのコールバックメソッド
        private bool EnumerateWindow(IntPtr hWnd, IntPtr lParam)
        {
            // ウィンドウが可視かどうか調べる
            if (IsWindowVisible(hWnd))
                // 可視の場合
                GetCaptionAndProcess(hWnd);

            // ウィンドウの列挙を継続するにはtrueを返す必要がある
            return true;
        }

        // ウィンドウのキャプションとプロセス名を表示する
        public void GetCaptionAndProcess(IntPtr hWnd)
        {
            var titles = new List<string>();

            // ウィンドウのキャプションを取得・表示
            StringBuilder caption = new StringBuilder(65535);

            GetWindowText(hWnd, caption, caption.Capacity);

            // ウィンドウハンドルからプロセスIDを取得
            int processId;

            GetWindowThreadProcessId(hWnd, out processId);
            // プロセスIDからProcessクラスのインスタンスを取得
            Process p = Process.GetProcessById(processId);

            if (p.ProcessName == ProcessName && !string.IsNullOrEmpty(caption.ToString()))
            {
                if (!Properties.Settings.Default.isCountingNotMinimized)
                {
                    Titles.Add(caption.ToString());
                }
                else if (Properties.Settings.Default.isCountingNotMinimized && !IsIconic(hWnd))
                {
                    Titles.Add(caption.ToString());
                }
            }
        }
    }

}