using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;
using System.Diagnostics;

static class WindowsHandles
{
    [DllImport("user32.dll")]
    private static extern IntPtr FindWindowEx(IntPtr hWnd, IntPtr hwndChildAfter, string lpszClass, string lpszWindow);
    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);
    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);
    [DllImport("user32.dll")]
    private static extern int GetWindowThreadProcessId(IntPtr hWnd, out int lpdwProcessId);
    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern int GetWindowTextLength(IntPtr hWnd);
    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(IntPtr hWnd, int hWndInsertAfter, int x, int y, int cx, int cy, int uFlags);
    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    public delegate bool EnumWindowsDelegate(IntPtr hWnd, IntPtr lparam);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private extern static bool EnumWindows(EnumWindowsDelegate lpEnumFunc, IntPtr lparam);

    // EnumWindowsから呼び出されるコールバック関数WNDENUMPROCのデリゲート
    private delegate bool WNDENUMPROC(IntPtr hWnd, IntPtr lParam);

    private static List<IntPtr> _hWndList = new List<IntPtr>();
    private static string _targetProcessName = null;
    private static string _oldProcessName = null;

    // Windowsクラス
    public class Window
    {
        public string ClassName;
        public string Title;
        public IntPtr hWnd;
    }

    // Windowsクラスの格納リスト
    public static List<List<Window>> WindowsList { get; set; }

    // 初期化
    public static void Initialize(string processName, bool isAll = false)
    {
        // キャッシュクリアしない場合、同じプロセス名なら画面情報を再利用する
        if (_oldProcessName == null || _oldProcessName.ToUpper() != processName.ToUpper())
        {
            // 初期化
            _hWndList.Clear();

            // 画面情報の取得
            _targetProcessName = processName;
            EnumWindows(new EnumWindowsDelegate(EnumWindowCallBack), IntPtr.Zero);

            WindowsList = new List<List<Window>>();
            foreach (IntPtr hWnd in _hWndList)
            {
                WindowsList.Add(GetAllChildWindows(GetWindow(hWnd), new List<Window>(), isAll));
            }

            _oldProcessName = processName;
        }
    }

    // タイトル存在チェック
    public static bool ExistsTitle(string title)
    {
        bool result = false;

        foreach (List<Window> win in WindowsList)
        {
            result = win.Any(x => x.Title == title);
            if (result) break;
        }

        return result;
    }

    // キャッシュをクリアする
    public static void Clear()
    {
        _oldProcessName = null;
        _hWndList = new List<IntPtr>();
    }

    // アクティブなプロセス名を取得する
    public static string GetActiveProcessName()
    {
        // 現在アクティブなプロセスIDとプロセス名を取得
        int processId;
        GetWindowThreadProcessId(GetForegroundWindow(), out processId);

        return Process.GetProcessById(processId).ProcessName;
    }

    // ウィンドウをアクティブにする
    public static void SetActiveWindow(IntPtr hWnd)
    {
        const int SWP_NOSIZE = 0x0001;
        const int SWP_NOMOVE = 0x0002;
        const int SWP_SHOWWINDOW = 0x0040;

        const int HWND_TOPMOST = -1;
        const int HWND_NOTOPMOST = -2;

        SetWindowPos(hWnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE);
        SetWindowPos(hWnd, HWND_NOTOPMOST, 0, 0, 0, 0, SWP_SHOWWINDOW | SWP_NOMOVE | SWP_NOSIZE);
    }

    // 指定したウィンドウの全ての子孫ウィンドウを取得し、リストに追加する
    private static List<Window> GetAllChildWindows(Window parent, List<Window> dest, bool isAll)
    {
        // タブコントロールなどで選択タブ以外は取得しない
        if (!isAll && !IsWindowVisible(parent.hWnd)) return dest;

        dest.Add(parent);
        EnumChildWindows(parent.hWnd).ToList().ForEach(x => GetAllChildWindows(x, dest, isAll));
        return dest;
    }

    // 与えた親ウィンドウの直下にある子ウィンドウを列挙する(孫ウィンドウは見つけてくれない)
    private static IEnumerable<Window> EnumChildWindows(IntPtr hParentWindow)
    {
        IntPtr hWnd = IntPtr.Zero;
        while ((hWnd = FindWindowEx(hParentWindow, hWnd, null, null)) != IntPtr.Zero) { yield return GetWindow(hWnd); }
    }

    // ウィンドウハンドルを渡すと、ウィンドウテキスト(ラベルなど)、クラス、スタイルを取得してWindowsクラスに格納して返す
    private static Window GetWindow(IntPtr hWnd)
    {
        int textLen = GetWindowTextLength(hWnd);
        string windowText = null;
        if (0 < textLen)
        {
            // ウィンドウのタイトルを取得する
            StringBuilder windowTextBuffer = new StringBuilder(textLen + 1);
            GetWindowText(hWnd, windowTextBuffer, windowTextBuffer.Capacity);
            windowText = windowTextBuffer.ToString();
        }

        // ウィンドウのクラス名を取得する
        StringBuilder classNameBuffer = new StringBuilder(256);
        GetClassName(hWnd, classNameBuffer, classNameBuffer.Capacity);

        return new Window() { hWnd = hWnd, Title = windowText, ClassName = classNameBuffer.ToString() };
    }

    // コールバック関数
    private static bool EnumWindowCallBack(IntPtr hWnd, IntPtr lparam)
    {
        // ウィンドウが可視かどうか調べる
        if (!IsWindowVisible(hWnd)) return true;

        // ウィンドウハンドルからプロセスIDを取得
        int processId;
        GetWindowThreadProcessId(hWnd, out processId);
        // プロセスIDからProcessクラスのインスタンスを取得
        if (_targetProcessName == "" ||
            Process.GetProcessById(processId).ProcessName.ToUpper() == _targetProcessName.ToUpper())
        {
            // ウィンドウのタイトルの長さを取得する
            int textLen = GetWindowTextLength(hWnd);
            if (0 < textLen)
            {
                _hWndList.Add(hWnd);
            }
        }

        return true;
    }
}