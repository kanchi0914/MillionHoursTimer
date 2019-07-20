using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using TM = System.Timers;
using System.Diagnostics;
using Microsoft.Win32;

namespace MHTimer
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

        private bool isSuspending = false;
        private bool isNoInputing = false;

        private int interval = Properties.Settings.Default.CountInterval;

        public TimeCounter(MainWindow mainWindow)
        {
            this.mainWindow = mainWindow;

            //システムのスリープ時処理
            SystemEvents.PowerModeChanged += (sender, e) =>
            {
                switch (e.Mode)
                {
                    //スリープ開始
                    case PowerModes.Suspend:
                        isSuspending = true;
                        mainWindow.ExitAllApp();
                        break;
                    //復帰
                    case PowerModes.Resume:
                        isSuspending = false;
                        break;
                }
            };

            interval = Properties.Settings.Default.CountInterval;
            CreateTimer();
        }

        /// <summary>
        /// タイマーの初期化
        /// </summary>
        private void CreateTimer()
        {
            timer = new TM.Timer();
            timer.Elapsed += new TM.ElapsedEventHandler(TimeDisp);
            timer.Interval = (Properties.Settings.Default.CountInterval * Settings.CountSeconds) * 1000;
            timer.AutoReset = true;
            timer.Enabled = true;
        }

        /// <summary>
        /// タイマーの設定更新
        /// </summary>
        public void UpdateTimer()
        {
            timer.Interval = (Properties.Settings.Default.CountInterval * Settings.CountSeconds) * 1000;
        }

        private void TimeDisp(object sender, EventArgs e)
        {
            //日付を更新
            mainWindow.UpdateDate();

            //無操作を確認
            CheckNoInput();

            //スリープ中、無操作時でない
            if (!isSuspending && !isNoInputing)
            {
                //計測
                Count();

                //終了確認
                CheckClosedApp();

                //データを保存
                mainWindow.SaveCsvData();

                //listviewの更新
                mainWindow.listView.Dispatcher.BeginInvoke(new Action(() => mainWindow.listView.Items.Refresh()));

                //ファイルデータの重複防止フラグをリセット
                ResetFileCount();
            }
        }

        #region 計測メソッド

        public void Count()
        {

            //アクティブウィンドウのみをカウント
            if (Settings.IsCountingOnlyActive)
            {
                CountOnlyActiveApp();
            }
            //最小化しているときはカウントしない
            else if (Settings.IsCountingNotMinimized)
            {
                CountNotMinimizedApp();
            }
            //すべてのアプリをカウント
            else
            {
                CountAllApps();
            }

            mainWindow.FileListWindows.ForEach(w => w.UpdateListView());

            //foreach (FileViewWindow window in mainWindow.FileListWindows)
            //{
            //    window.UpdateListView();
            //}
        }

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
                        if (p.MainWindowHandle != IntPtr.Zero && !IsIconic(p.MainWindowHandle))
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
                    AppDataObject data = mainWindow.AppDatas.Find(a => a.ProcessName == processName);
                    //found registerd app
                    if (data != null)
                    {
                        data.AccumulateMinutes();
                        data.AccumulateMinuteToFileData(sb.ToString());
                    }
                }
            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        #endregion

        /// <summary>
        /// ファイルデータの重複防止フラグをリセット
        /// </summary>
        public void ResetFileCount()
        {
            foreach (AppDataObject appData in mainWindow.AppDatas)
            {
                foreach (AppDataObject.FileData fileData in appData.Files)
                {
                    fileData.IsCounted = false;
                }
            }
        }

        /// <summary>
        /// PC操作がない場合、計測を終了
        /// </summary>
        public void CheckNoInput()
        {
            if (LastInputCounter.GetLastInputMinutes() >= Settings.NoInputTime)
            {
                mainWindow.ExitAllApp();
                isNoInputing = true;
            }
            else
            {
                isNoInputing = false;
            }
        }

        /// <summary>
        /// 計測中のアプリで、終了したものがないか確認
        /// </summary>
        public void CheckClosedApp()
        {
            foreach (AppDataObject appData in mainWindow.AppDatas)
            {
                if (appData.IsRecordStarted)
                {
                    //記録していたアプリが終了した
                    if (appData.IsRunning && (DateTime.Now - appData.LastTime).TotalMinutes > Properties.Settings.Default.CountInterval)
                    {
                        appData.Exit();
                    }
                }
            }
        }


    }
}
