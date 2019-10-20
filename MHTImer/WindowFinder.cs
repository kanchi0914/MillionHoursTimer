using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace MHTimer
{
    public static class WindowFinder
    {
        [System.Runtime.InteropServices.DllImport("user32.dll",
                CharSet = System.Runtime.InteropServices.CharSet.Auto)]
        static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        static extern IntPtr FindWindowEx(IntPtr hWndParent, IntPtr hWndChildAfter, string lpClassName, string lpWindowName);

        [DllImport("user32.dll", SetLastError = true)]
        static extern IntPtr GetWindow(IntPtr hWnd, int uCmd);

        [System.Runtime.InteropServices.DllImport("user32.dll",
                        CharSet = System.Runtime.InteropServices.CharSet.Auto)]
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

                //出力
                //Console.WriteLine(title);
                //出力
            }
        }
    }
}
