using System;
using System.Runtime.InteropServices;
using System.Text;

namespace MHTimer
{
    public static class WindowFinder
    {
        [DllImport("user32.dll",
                CharSet = CharSet.Auto)]
        static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        static extern IntPtr FindWindowEx(IntPtr hWndParent, IntPtr hWndChildAfter, string lpClassName, string lpWindowName);

        [DllImport("user32.dll", SetLastError = true)]
        static extern IntPtr GetWindow(IntPtr hWnd, int uCmd);

        [DllImport("user32.dll",
                        CharSet = CharSet.Auto)]
        static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int cch);

        public static void Find()
        {
            //メモ帳のウィンドウを探す
            IntPtr hWnd = FindWindow("Notepad", null);
            if (hWnd != null)
            {
                //ウィンドウタイトルを取得
                StringBuilder title = new StringBuilder(256);
                int titleLen = GetWindowText(hWnd, title, 256);
            }
        }
    }
}
