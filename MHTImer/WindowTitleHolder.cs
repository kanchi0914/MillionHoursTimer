using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace MHTimer
{
    public class WindowTitleHolder
    {
        private MainWindow mainWindow;
        private List<IntPtr> handles = new List<IntPtr>();

        private IDictionary<IntPtr, string> handleDicts; 

        public WindowTitleHolder(MainWindow mainWindow)
        {
            this.mainWindow = mainWindow;
        }

        public void Enumerate()
        {
            handleDicts = OpenWindowGetter.GetOpenWindows(mainWindow);
        }

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        static extern IntPtr FindWindowEx(IntPtr hWndParent, IntPtr hWndChildAfter, string lpClassName, string lpWindowName);

        //ref:https://smdn.jp/programming/tips/enumwindows/
        //ref:https://dobon.net/vb/dotnet/process/enumwindows.html
        // ウィンドウを列挙するためのコールバックメソッド
        private bool EnumerateWindow(IntPtr hWnd, IntPtr lParam)
        {
            if (WinAPI.IsWindowVisible(hWnd))
            {
                int processId;
                WinAPI.GetWindowThreadProcessId(hWnd, out processId);
                var process = Process.GetProcessById(processId);

                var isExistsProcess = mainWindow.AppDatas.Any(a => a.ProcessName == process.ProcessName);
                if (isExistsProcess) handles.Add(hWnd);
            }
            return true;
        }

        public List<string> GetWindowTitlesByProcessName(string processName)
        {
            var matchedHandles = new List<IntPtr>();
            var titles = new List<string>();

            foreach (var handle in handleDicts.Keys)
            {
                if (handleDicts[handle] != processName) continue;
               
                var caption = new StringBuilder(256);
                WinAPI.GetWindowText(handle, caption, caption.Capacity);
                if (Settings.IsIgnoringChindWindowSettings)
                {
                    titles.Add(caption.ToString());
                }
                else if (!Properties.Settings.Default.IsCountingNotMinimized &&
                    !Settings.IsCountingOnlyActive)
                {
                    titles.Add(caption.ToString());
                }
                else if (Properties.Settings.Default.IsCountingNotMinimized)
                {
                    if (!WinAPI.IsIconic(handle)) titles.Add(caption.ToString());
                }
                else if (Settings.IsCountingOnlyActive)
                {
                    var cap = caption.ToString();
                    var foregroundCap = GetForegroundWindowTitle();
                    if (cap == foregroundCap)
                    {
                        titles.Add(caption.ToString());
                    }
                }
            }
            return titles;
        }

        public string GetForegroundWindowTitle()
        {
            StringBuilder caption = new StringBuilder(256);
            var handle = WinAPI.GetForegroundWindow();
            WinAPI.GetWindowText(handle, caption, caption.Capacity);
            return caption.ToString();
        }

    }
}
