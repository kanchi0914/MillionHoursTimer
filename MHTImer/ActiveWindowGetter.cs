using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace MHTimer
{
    public static class ActiveWindowGetter
    {
        /// Win32API の extern 宣言クラス
        public class WinAPI
        {
            [DllImport("user32.dll")]
            public static extern IntPtr GetForegroundWindow();

            [DllImport("user32.dll")]
            public static extern int GetWindowThreadProcessId(IntPtr hWnd, out int lpdwProcessId);
        }

        public static Process GetActiveProcess()
        {
            // アクティブなウィンドウハンドルの取得
            IntPtr hWnd = WinAPI.GetForegroundWindow();
            int id;
            // ウィンドウハンドルからプロセスIDを取得
            WinAPI.GetWindowThreadProcessId(hWnd, out id);
            Process process = Process.GetProcessById(id);
            return process;
        }
    }
}
