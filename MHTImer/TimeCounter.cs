using System;
using System.Linq;
using System.Text;
using TM = System.Timers;
using System.Diagnostics;
using Microsoft.Win32;
using static MHTimer.WinAPI;

namespace MHTimer
{
    public class TimeCounter
    {
        private MainWindow mainWindow;

        private TM.Timer timer;

        //スリープ中かどうか
        private bool isSleeping = false;
        //操作無し状態かどうか
        private bool isNoInputing = false;

        private int countInterval = Properties.Settings.Default.CountingSecondsInterval;

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
                        isSleeping = true;
                        mainWindow.ExitAllApps();
                        break;
                    //復帰
                    case PowerModes.Resume:
                        isSleeping = false;
                        break;
                }
            };

            countInterval = Properties.Settings.Default.CountingSecondsInterval;
            CreateTimer();
        }

        /// <summary>
        /// タイマーの初期化
        /// </summary>
        private void CreateTimer()
        {
            timer = new TM.Timer();
            timer.Elapsed += new TM.ElapsedEventHandler(TimeDisp);
            timer.Interval = (Settings.CountingSecondsInterval) * 1000;
            timer.AutoReset = true;
            timer.Enabled = true;
        }

        /// <summary>
        /// タイマーの設定更新
        /// </summary>
        public void UpdateTimer()
        {
            timer.Interval = (Settings.CountingSecondsInterval) * 1000;
        }

        private void TimeDisp(object sender, EventArgs e)
        {
            //日付を更新
            mainWindow.UpdateDateOfAppDatas();

            //無操作を確認
            CheckNoInput();

            if (isSleeping && Settings.StopsOnSleep) return;

            //スリープ中や無操作時でない
            if (!isSleeping && !isNoInputing)
            {
                mainWindow.WindowTitleHolder.Enumerate();
                WindowFinder.Find();

                lock (mainWindow.AppDatas)
                {
                    Count();
                }

                lock (mainWindow.AppDatas)
                {
                    ExitClosedApps();
                }

                //データを保存
                mainWindow.SaveAndLoader.SaveCsvData();

            }
            mainWindow.ListViewSetter.UpdateListView();
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
                CountNotMinimizedApps();
            }
            //すべてのアプリをカウント
            else
            {
                CountAllApps();
            }

            mainWindow.FileViewWindows.ForEach(w => w.UpdateListView());
        }

        public void CountAllApps()
        {
            foreach (AppDataObject data in mainWindow.AppDatas)
            {
                Process[] processes = Process.GetProcessesByName(data.ProcessName);
                if (processes.Length > 0)
                {
                    data.AccumulateTimes();
                }
            }
        }

        public void CountNotMinimizedApps()
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
                                data.AccumulateTimes();
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
                    if (Properties.Settings.Default.IsCountingNotMinimized)
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
                    AppDataObject data = mainWindow.AppDatas.ToList().Find(a => a.ProcessName == processName);
                    if (data != null)
                    {
                        data.AccumulateTimes();
                    }
                }
            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                ErrorLogger.Log(ex);
            }
        }

        #endregion

        /// <summary>
        /// PC操作がない場合、計測を終了
        /// </summary>
        public void CheckNoInput()
        {
            if (LastInputCounter.GetLastInputSeconds() >= Settings.NoInputTime)
            {
                mainWindow.ExitAllApps();
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
        public void ExitClosedApps()
        {
            var sec = Properties.Settings.Default.CountingSecondsInterval;
            var runningApps = mainWindow.AppDatas.Where(a => a.IsRecoding && a.IsRunning).ToList();
            var closedApps = runningApps.Where(a => (DateTime.Now - a.LastRunningTime).TotalSeconds > sec).ToList();
            closedApps.ForEach(a => a.Exit());
        }
    }
}
