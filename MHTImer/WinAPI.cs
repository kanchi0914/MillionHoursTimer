using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace MHTimer
{
    public static class WinAPI
    {
        [DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();

        //[DllImport("user32")]
        //public static extern bool EnumWindows(WNDENUMPROC lpEnumFunc, IntPtr lParam);

        //public delegate bool WNDENUMPROC(IntPtr hWnd, IntPtr lParam);

        public delegate bool EnumWindowsProc(IntPtr hWnd, int lParam);

        [DllImport("user32.dll")]
        public static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern int GetWindowThreadProcessId(IntPtr hWnd, out int lpdwProcessId);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool IsIconic(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern bool EnumWindows(EnumWindowsProc enumFunc, int lParam);

        [DllImport("user32.dll")]
        public static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll")]
        public static extern int GetWindowTextLength(IntPtr hWnd);

        [DllImport("USER32.DLL")]
        public static extern IntPtr GetShellWindow();
    }
}
