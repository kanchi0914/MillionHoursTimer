using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Diagnostics;
using System.Linq;
using Microsoft.Win32;
using Microsoft.WindowsAPICodePack.Dialogs;

namespace MHTimer
{

    public partial class MainWindow : Window
    {

        struct TitleAndProcess
        {
            public string title;
            public Process process;

            public TitleAndProcess(string t, Process p)
            {
                title = t;
                process = p;
            }
        }

        public List<AppDataObject> AppDatas { get; set; } = new List<AppDataObject>();
        public List<FileViewWindow> FileViewWindows { get; set; } = new List<FileViewWindow>();
        public SettingWindow SettingMenuWindow { get; set; }

        public TimeCounter TimeCounter { get; set; }
        public TogglManager TogglManager { get; set; }
        public NotifyIconSetter NotifyIconSetter { get; set; }
        public ListViewSetter ListViewSetter { get; set; }
        public SaveAndLoader SaveAndLoader { get; set; }

        public MainWindow()
        {

            InitializeComponent();

            SaveAndLoader = new SaveAndLoader(this);

            //データの読み込み
            SaveAndLoader.LoadData();

            TimeCounter = new TimeCounter(this);
            TogglManager = new TogglManager(this);
            SettingMenuWindow = new SettingWindow(this);
            NotifyIconSetter = new NotifyIconSetter(this);
            ListViewSetter = new ListViewSetter(this);

            //日付を確認し、今日の日付と違っていれば更新
            UpdateDate();

            //メニューの作成
            CreateMenu();

            SetView();
            SetEvents();

        }

        private void SetEvents()
        {
            //アプリ終了時のイベントを登録
            Closed += (s, e) => OnExit();

            //Windows終了時のイベントを追加
            SystemEvents.SessionEnding += new SessionEndingEventHandler(SystemEvents_SessionEnding);
        }

        private void SetView()
        {
            //タスクバーに表示されないようにする
            ShowInTaskbar = false;

            if (Settings.IsLaunchedFromConsole)
            {
                WindowState = WindowState.Minimized;
            }
        }

        /// <summary>
        /// 日付を確認し、今日の日付と違っていれば更新
        /// </summary>
        public void UpdateDate()
        {
            string currentDate = DateTime.Now.ToString("yyyy/MM/dd");
            if (Settings.Date != currentDate)
            {
                Settings.Date = currentDate;
                foreach (AppDataObject appData in AppDatas)
                {
                    appData.TodaysMinutes = 0;
                }
            }
        }

        /// <summary>
        /// 全ての記録情報を終了
        /// </summary>
        public void ExitAllApp()
        {
            foreach (AppDataObject appData in AppDatas)
            {
                appData.Exit();
            }
        }

        /// <summary>
        /// メニューバー>設定　メニュー　クリック時に呼ばれる
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void topMenu_Click(object sender, RoutedEventArgs e)
        {
            if (!SettingMenuWindow.IsActive)
            {
                SettingMenuWindow = new SettingWindow(this);
                SettingMenuWindow.ShowDialog();
            }
        }

        /// <summary>
        /// メインウィンドウを閉じたときに呼ばれ，閉じるのをキャンセルし最小化
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        protected virtual void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            try
            {
                //閉じるのをキャンセルする
                e.Cancel = true;

                //ウィンドウを非可視にする
                Visibility = Visibility.Collapsed;
            }
            catch { }
        }

        /// <summary>
        /// アプリ終了時に呼ばれ、データを保存
        /// </summary>
        private void OnExit()
        {
            ExitAllApp();
            SaveAndLoader.SaveCsvData();
        }

        /// <summary>
        /// シャットダウン時に呼ばれ、データを保存
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SystemEvents_SessionEnding(object sender, SessionEndingEventArgs e)
        {
            OnExit();
        }

        #region メニューバー>ファイル メニューの設定

        /// <summary>
        /// メニューバーを設定する
        /// </summary>
        public void CreateMenu()
        {
            AddApp.Click += OnClickAddApp;
            //Import.Click += OnClickImportData;
            Export.Click += OnClickExportData;
        }

        /// <summary>
        /// メニュー>計測するアプリケーションの追加　をクリック時に呼ばれる
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnClickAddApp(object sender, RoutedEventArgs e)
        {
            string path = GetFilePathByFileDialog();

            if (!string.IsNullOrEmpty(path))
            {
                ListViewSetter.AddListFromPath(path);
            }
        }

        /// <summary>
        /// メニュー>データのインポート　をクリック時に呼ばれる
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        //public void OnClickImportData(object sender, RoutedEventArgs e)
        //{
        //    string path = GetFolderPathByFileDialog();
        //    if (!string.IsNullOrEmpty(path))
        //    {
        //        var splitted = path.Split('\\');
        //        if (splitted.Last() != "data")
        //        {
        //            MessageBox.Show("dataフォルダを選択してください",
        //                "エラー",
        //                MessageBoxButton.OK,
        //                MessageBoxImage.Error);
        //            return;
        //        }

        //        DirectoryProcessor.CopyAndReplace(@path, "data");
        //    }
        //}

        /// <summary>
        /// メニュー>データのエクスポート　をクリック時に呼ばれる
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public void OnClickExportData(object sender, RoutedEventArgs e)
        {
            string path = GetFolderPathByFileDialog();

            path += "\\data";

            if (!string.IsNullOrEmpty(path))
            {
                DirectoryProcessor.CopyAndReplace("data", @path);
            }
        }

        /// <summary>
        /// ファイルダイアログを表示し、.exeファイルを選択させる
        /// </summary>
        /// <returns></returns>
        private string GetFilePathByFileDialog()
        {
            var dialog = new OpenFileDialog();
            dialog.Title = "実行ファイル(.exe)を選択してください";
            dialog.Filter = "実行ファイル(*.exe)|*.exe";
            if (dialog.ShowDialog() == true)
            {
                return dialog.FileName;
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// ファイルダイアログを表示し、フォルダを選択させる
        /// </summary>
        /// <param name="title"></param>
        /// <returns></returns>
        private string GetFolderPathByFileDialog(string title = "フォルダを選択してください")
        {
            var dialog = new CommonOpenFileDialog(title);
            dialog.IsFolderPicker = true;
            var ret = dialog.ShowDialog();
            if (ret == CommonFileDialogResult.Ok)
            {
                return dialog.FileName;
            }
            else
            {
                return null;
            }
        }
        #endregion

        /// <summary>
        /// アプリケーションのデータを削除
        /// </summary>
        /// <param name="obj"></param>
        public void RemoveAppData(AppDataObject obj)
        {
            AppDatas.Remove(obj);
            listView.Items.Remove(obj);
            listView.Items.Refresh();
            SaveAndLoader.SaveCsvData();
        }

        private void InitAppDatas()
        {
            AppDatas.ForEach(a => a.Init());
        }

        private void listView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }

        #region ドラッグ＆ドロップ処理

        private void Window_Drop(object sender, DragEventArgs e)
        {
            string[] files = e.Data.GetData(DataFormats.FileDrop) as string[];
            string text = "";
            if (files != null)
            {
                files.ToList().ForEach(s => text += s + "\n");
                files.ToList().ForEach(s => ListViewSetter.SetFilePathFromDroppedLinks(s));
            }
        }
        private void Window_PreviewDragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop, true))
                e.Effects = DragDropEffects.Copy;
            else
                e.Effects = DragDropEffects.None;
            e.Handled = true;
        }

        #endregion

    }

}


