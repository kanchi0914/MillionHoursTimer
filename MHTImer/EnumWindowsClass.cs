using System;
using System.Text;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Collections;

namespace MHTimer
{
    public static class EnumWindowsClass
    {
        public static Process[] GetProcessesByWindow(
    string windowText, string className)
        {
            //検索の準備をする
            foundProcesses = new ArrayList();
            foundProcessIds = new ArrayList();
            searchWindowText = windowText;
            searchClassName = className;

            //ウィンドウを列挙して、対象のプロセスを探す
            EnumWindows(new EnumWindowsDelegate(EnumWindowCallBack), IntPtr.Zero);

            //結果を返す
            return (Process[])foundProcesses.ToArray(typeof(Process));
        }

        private static string searchWindowText = null;
        private static string searchClassName = null;
        private static ArrayList foundProcessIds = null;
        private static ArrayList foundProcesses = null;

        private delegate bool EnumWindowsDelegate(IntPtr hWnd, IntPtr lparam);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private extern static bool EnumWindows(EnumWindowsDelegate lpEnumFunc,
            IntPtr lparam);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern int GetWindowText(IntPtr hWnd,
            StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern int GetWindowTextLength(IntPtr hWnd);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern int GetClassName(IntPtr hWnd,
            StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowThreadProcessId(
            IntPtr hWnd, out int lpdwProcessId);

        private static bool EnumWindowCallBack(IntPtr hWnd, IntPtr lparam)
        {
            if (searchWindowText != null)
            {
                //ウィンドウのタイトルの長さを取得する
                int textLen = GetWindowTextLength(hWnd);
                if (textLen == 0)
                {
                    //次のウィンドウを検索
                    return true;
                }
                //ウィンドウのタイトルを取得する
                StringBuilder tsb = new StringBuilder(textLen + 1);
                GetWindowText(hWnd, tsb, tsb.Capacity);
                //タイトルに指定された文字列を含むか
                if (tsb.ToString().IndexOf(searchWindowText) < 0)
                {
                    //含んでいない時は、次のウィンドウを検索
                    return true;
                }
            }

            if (searchClassName != null)
            {
                //ウィンドウのクラス名を取得する
                StringBuilder csb = new StringBuilder(256);
                GetClassName(hWnd, csb, csb.Capacity);
                //クラス名に指定された文字列を含むか
                if (csb.ToString().IndexOf(searchClassName) < 0)
                {
                    //含んでいない時は、次のウィンドウを検索
                    return true;
                }
            }

            //プロセスのIDを取得する
            int processId;
            GetWindowThreadProcessId(hWnd, out processId);
            //今まで見つかったプロセスでは無いことを確認する
            if (!foundProcessIds.Contains(processId))
            {
                foundProcessIds.Add(processId);
                //プロセスIDをからProcessオブジェクトを作成する
                foundProcesses.Add(Process.GetProcessById(processId));
            }

            //次のウィンドウを検索
            return true;
        }
    }
}
