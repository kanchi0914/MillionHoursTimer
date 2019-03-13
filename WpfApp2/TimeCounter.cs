using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using TM = System.Timers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Drawing;
using System.Diagnostics;

using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using System.Runtime.InteropServices;

namespace WpfApp2
{
    public class TimeCounter
    {

        private MainWindow mainWindow;
        //List<TitleAndProcess> processes = new List<TitleAndProcess>();

        [DllImport("user32")]
        private static extern bool EnumWindows(WNDENUMPROC lpEnumFunc, IntPtr lParam);

        private delegate bool WNDENUMPROC(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", EntryPoint = "GetWindowText", CharSet = CharSet.Auto)]
        public static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out int lpdwProcessId);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool IsIconic(IntPtr hWnd);

        public TimeCounter(MainWindow mainWindow)
        {
            this.mainWindow = mainWindow;
            CreateTimer();
        }

        //直す
        private void CreateTimer()
        {
            TM.Timer timer = new TM.Timer();
            timer.Elapsed += new TM.ElapsedEventHandler(TimeDisp);
            //timer.Interval = (mainWindow.CountMinutes * 60) * (int)mainWindow.CountMinutes * 1000;
            timer.Interval = (mainWindow.CountMinutes * 60) * (int)mainWindow.CountMinutes * 1000;
            timer.AutoReset = true;
            timer.Enabled = true;
        }

        private void TimeDisp(object sender, EventArgs e)
        {

            List<AppDataObject> tempAppData = new List<AppDataObject>(mainWindow.AppDatas);

            //すべてのアプリをカウント
            if (!mainWindow.IsCountingMinimized && !mainWindow.IsCountingOnlyActive)
            {
                CountAllApps();
            }
            //最小化しているときはカウントしない
            else if (mainWindow.IsCountingMinimized)
            {
                CountNotMinimizedApp();
            }
            //アクティブウィンドウのみをカウント
            else if (mainWindow.IsCountingOnlyActive)
            {
                CountOnlyActiveApp();
            }

            foreach (FileViewWindow window in mainWindow.FileListWindows)
            {
                window.Update();
            }

            //終了確認
            CheckClosedApp();

            //データを保存
            mainWindow.SaveCsvData();

            //listviewの更新
            mainWindow.listView.Dispatcher.BeginInvoke(new Action(() => mainWindow.listView.Items.Refresh()));

        }

        public void CountAllApps()
        {
            foreach (AppDataObject data in mainWindow.AppDatas)
            {
                Process[] processes = Process.GetProcessesByName(data.ProcessName);

                if (processes.Length > 0)
                {
                    data.AddMinute();
                    foreach (Process p in processes)
                    {
                        data.AddMinuteToFiles(p);
                    }
                }
            }
        }

        public void CountNotMinimizedApp()
        {
            foreach (AppDataObject data in mainWindow.AppDatas)
            {
                bool isCounted = false;
                Process[] processes = Process.GetProcessesByName(data.ProcessName);

                if (processes.Length > 0)
                {
                    foreach (Process p in processes)
                    {
                        if (p.MainWindowHandle != IntPtr.Zero && !IsIconic(p.MainWindowHandle))
                        {
                            if (!isCounted)
                            {
                                data.AddMinute();
                                isCounted = true;
                            }
                            data.AddMinuteToFiles(p);
                        }
                    }
                }
            }
        }

        public void CountOnlyActiveApp()
        {
            string processName = "";
            StringBuilder sb = new StringBuilder(65535);
            GetWindowText(GetForegroundWindow(), sb, 65535);
            IntPtr hWnd = GetForegroundWindow();
            int processid;

            try
            {
                GetWindowThreadProcessId(hWnd, out processid);
                if (0 != processid)
                {
                    Process p = Process.GetProcessById(processid);
                    if (mainWindow.IsCountingMinimized)
                    {
                        if (p.MainWindowHandle != IntPtr.Zero && !IsIconic(p.MainWindowHandle))
                        {
                            processName = p.ProcessName.ToString();
                        }
                    }
                    else
                    {
                        processName = p.ProcessName.ToString();
                    }
                    Console.WriteLine(processName);
                    AppDataObject data = mainWindow.AppDatas.Find(a => a.ProcessName == processName);
                    //found registerd app
                    if (data != null)
                    {
                        data.AddMinute();
                        data.AddMinuteToFiles(p);
                    }
                }
            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        public void CheckClosedApp()
        {
            foreach (AppDataObject data in mainWindow.AppDatas)
            {
                //記録していたアプリが終了した
                if (data.IsLaunched && (DateTime.Now - data.LastTime).TotalMinutes > mainWindow.CountMinutes)
                {
                    data.IsLaunched = false;
                    Console.WriteLine($"exit {data.DisplayedName}");
                    mainWindow.togglManager.SetTimeEntry(data);
                }
            }
        }


    }
}
