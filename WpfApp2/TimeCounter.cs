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

        [DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", EntryPoint = "GetWindowText", CharSet = CharSet.Auto)]
        public static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out int lpdwProcessId);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool IsIconic(IntPtr hWnd);

        private TM.Timer timer;

        private int interval = Properties.Settings.Default.CountInterval;

        public TimeCounter(MainWindow mainWindow)
        {
            this.mainWindow = mainWindow;

            interval = Properties.Settings.Default.CountInterval;
            CreateTimer();


            //Test();
            //Console.WriteLine("");

        }


        private void Test()
        {
            foreach (AppDataObject data in mainWindow.AppDatas)
            {

                Process[] processes = Process.GetProcessesByName(data.ProcessName);

                //if (data.ProcessName == "CLIPStudioPaint")
                //{
                //    Console.WriteLine(processes);
                //}




                if (processes.Length > 0)
                {

                }
            }
        }

        //直す
        private void CreateTimer()
        {
            //TM.Timer timer = new TM.Timer();
            timer = new TM.Timer();
            timer.Elapsed += new TM.ElapsedEventHandler(TimeDisp);
            //timer.Interval = (mainWindow.CountMinutes * 60) * (int)mainWindow.CountMinutes * 1000;
            //timer.Interval = (interval * Settings.CountSeconds) * 1000;
            timer.Interval = (Properties.Settings.Default.CountInterval * SettingsAndUtilities.CountSeconds) * 1000;
            timer.AutoReset = true;
            timer.Enabled = true;
        }

        public void UpdateTimer()
        {
            timer.Interval = (Properties.Settings.Default.CountInterval * SettingsAndUtilities.CountSeconds) * 1000;
        }

        private void TimeDisp(object sender, EventArgs e)
        {

            List<AppDataObject> tempAppData = new List<AppDataObject>(mainWindow.AppDatas);

            //すべてのアプリをカウント
            if (!Properties.Settings.Default.isCountingNotMinimized &&
                !Properties.Settings.Default.isCountingOnlyActive)
            {
                CountAllApps();
            }
            //最小化しているときはカウントしない
            else if (Properties.Settings.Default.isCountingNotMinimized)
            {
                CountNotMinimizedApp();
            }
            //アクティブウィンドウのみをカウント
            else if (Properties.Settings.Default.isCountingOnlyActive)
            {
                CountOnlyActiveApp();
            }

            Test();


            foreach (FileViewWindow window in mainWindow.FileListWindows)
            {
                window.Update();
            }

            //Console.WriteLine(mainWindow.AppDatas);

            //終了確認
            CheckClosedApp();

            //データを保存
            mainWindow.SaveCsvData();

            //listviewの更新
            mainWindow.listView.Dispatcher.BeginInvoke(new Action(() => mainWindow.listView.Items.Refresh()));

            //ファイルデータの重複防止フラグをリセット
            ResetFileCount();

        }

        #region 計測メソッド

        public void CountAllApps()
        {
            foreach (AppDataObject data in mainWindow.AppDatas)
            {
                Process[] processes = Process.GetProcessesByName(data.ProcessName);
                if (processes.Length > 0)
                {
                    data.AccumulateMinutes();
                    data.AccumulateMinuteToFileDatas();
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
                    //Console.WriteLine(processes.Length);
                    foreach (Process p in processes)
                    {
                        //  if (p.MainWindowHandle != IntPtr.Zero && !IsIconic(p.MainWindowHandle))
                        if (true)
                        {
                            if (!isCounted)
                            {
                                data.AccumulateMinutes();
                                data.AccumulateMinuteToFileDatas();
                                isCounted = true;
                            }
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
                    if (Properties.Settings.Default.isCountingNotMinimized)
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
                    //Console.WriteLine(processName);
                    AppDataObject data = mainWindow.AppDatas.Find(a => a.ProcessName == processName);
                    //found registerd app
                    if (data != null)
                    {
                        data.AccumulateMinutes();
                        data.AccumulateMinuteToFileData(sb.ToString());
                        //data.AddMinuteToFiles(p.ProcessName);
                    }
                }
            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        #endregion

        public void ResetFileCount()
        {
            foreach (AppDataObject appDataObject in mainWindow.AppDatas)
            {
                foreach (AppDataObject.FileData fileData in appDataObject.Files)
                {
                    fileData.IsCounted = false;
                }
            }
        }

        public void CheckClosedApp()
        {
            foreach (AppDataObject data in mainWindow.AppDatas)
            {
                //記録していたアプリが終了した
                if (data.IsCountStarted)
                {
                    if (data.IsRunning && (DateTime.Now - data.LastTime).TotalMinutes > Properties.Settings.Default.CountInterval)
                    {
                        data.IsRunning = false;
                        Console.WriteLine($"exit {data.DisplayedName}");
                        mainWindow.TogglManager.SetTimeEntry(data);
                    }
                }
            }
        }


    }
}
