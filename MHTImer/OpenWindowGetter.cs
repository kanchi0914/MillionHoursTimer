using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using static MHTimer.WinAPI;

//ref:https://src-bin.com/ja/q/6ee7ce
/// <summary>Contains functionality to get all the open windows.</summary>

namespace MHTimer
{
    public static class OpenWindowGetter
    {
        /// <summary>Returns a dictionary that contains the handle and title of all the open windows.</summary>
        /// <returns>A dictionary that contains the handle and title of all the open windows.</returns>
        public static IDictionary<IntPtr, string> GetOpenWindows(MainWindow mainWindow)
        {
            IntPtr shellWindow = GetShellWindow();
            Dictionary<IntPtr, string> windows = new Dictionary<IntPtr, string>();

            EnumWindows(delegate (IntPtr hWnd, int lParam)
            {
                if (hWnd == shellWindow) return true;
                if (!IsWindowVisible(hWnd)) return true;

                int length = GetWindowTextLength(hWnd);
                if (length == 0) return true;

                StringBuilder builder = new StringBuilder(length);
                GetWindowText(hWnd, builder, length + 1);

                int processId;
                WinAPI.GetWindowThreadProcessId(hWnd, out processId);
                var process = Process.GetProcessById(processId);

                lock (mainWindow.AppDatas)
                {
                    var isExistsProcess = mainWindow.AppDatas.Any(a => a.ProcessName == process.ProcessName);
                    if (isExistsProcess)
                    {
                        windows[hWnd] = process.ProcessName;
                    };
                }
                return true;

            }, 0);

            return windows;
        }
    }
}