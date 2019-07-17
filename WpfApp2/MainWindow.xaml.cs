using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Drawing;
using System.Diagnostics;
using Microsoft.Win32;
using System.IO;
using Microsoft.WindowsAPICodePack.Dialogs;
using Path = System.IO.Path;

namespace WpfApp2
{

    public partial class MainWindow : Window
    {
        private System.Windows.Forms.NotifyIcon _notifyIcon;

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
        public List<FileViewWindow> FileListWindows { get; set; } = new List<FileViewWindow>();
        public SettingWindow SettingMenuWindow { get; set; }

        public TimeCounter timeCounter;
        public TogglManager TogglManager;

        public MainWindow()
        {

            //タスクバーに表示されないようにする
            ShowInTaskbar = false;

            //タスクトレイアイコンの設定
            InitNotifyIcon();

            InitializeComponent();

            //アプリ終了時のイベントを登録
            Closed += (s, e) => OnExit();

            //Windows終了時のイベントを追加
            SystemEvents.SessionEnding += new SessionEndingEventHandler(SystemEvents_SessionEnding);

            //設定の読み込み
            //Settings.Load();
            
            //データの読み込み
            LoadCsvData();

            //設定画面
            timeCounter = new TimeCounter(this);
            TogglManager = new TogglManager(this);
            SettingMenuWindow = new SettingWindow(this);

            //日付を確認し、今日の日付と違っていれば更新
            UpdateDate();

            //メニューの作成
            CreateMenu();

            //右クリックメニューの作成
            CreateContextMenu();

            //アプリのリストビューを初期化
            try
            {
                InitListView();
            }
            catch (Exception ex)
            {
                //MessageBox.Show(ex.ToString());
                Console.WriteLine(ex);
            }

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
            SaveCsvData();
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

        #region タスクトレイアイコン

        /// <summary>
        /// タスクトレイアイコンを設定する
        /// </summary>
        private void InitNotifyIcon()
        {
            _notifyIcon = new System.Windows.Forms.NotifyIcon();
            _notifyIcon.Text = "MHTimer";
            var iconFilePath = AppDomain.CurrentDomain.SetupInformation.ApplicationBase
                + "/Resources/clockIcon.ico";
            
            // TODO: need to fix?

            //var iconFilePath = Settings.CurrentDir + "/Resources/clockIcon.ico";
            //Console.WriteLine(a);
            //Console.WriteLine(@iconFilePath);
            //_notifyIcon.Icon = new Icon("Resources/clockIcon.ico");
            _notifyIcon.Icon = new Icon(@iconFilePath);
            _notifyIcon.Visible = true;

            System.Windows.Forms.ContextMenuStrip menuStrip = new System.Windows.Forms.ContextMenuStrip();

            System.Windows.Forms.ToolStripMenuItem exitItem = new System.Windows.Forms.ToolStripMenuItem();
            exitItem.Text = "終了";
            menuStrip.Items.Add(exitItem);
            exitItem.Click += new EventHandler(exitItem_Click);

            _notifyIcon.ContextMenuStrip = menuStrip;

            //タスクトレイアイコンのクリックイベントハンドラを登録する
            _notifyIcon.MouseClick += new System.Windows.Forms.MouseEventHandler(_notifyIcon_MouseClick);
        }

        /// <summary>
        /// タスクトレイアイコンをクリック時に呼ばれる
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void _notifyIcon_MouseClick(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            try
            {
                if (e.Button == System.Windows.Forms.MouseButtons.Left)
                {
                    //ウィンドウを可視化
                    Visibility = Visibility.Visible;
                    WindowState = WindowState.Normal;
                    Activate();
                }
            }
            catch { }
        }

       /// <summary>
       /// アイコンの右クリックメニュー『終了』選択時に呼ばれる
       /// </summary>
       /// <param name="sender"></param>
       /// <param name="e"></param>
        private void exitItem_Click(object sender, EventArgs e)
        {
            //OnClickExit();
            string text = "";
            text = "終了してよろしいですか？";
            MessageBoxResult res = MessageBox.Show(text, "Confirmation", MessageBoxButton.OKCancel,
                        MessageBoxImage.Question, MessageBoxResult.Cancel);
            if (res == MessageBoxResult.Cancel)
            {
                return;
            }

            foreach (Window w in FileListWindows)
            {
                w.Close();
            }

            //アイコン表示を終了
            _notifyIcon.Dispose();

            Application.Current.Shutdown();

            Settings.Save();
        }

        #endregion


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
                AddListFromPath(path);
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

        #region 右クリックメニュー

        private void CreateContextMenu()
        {
            //右クリックメニュー
            //MenuItem menuItem = new MenuItem();
            MenuItem menuItem0 = new MenuItem();
            MenuItem menuItem1 = new MenuItem();
            MenuItem menuItem2 = new MenuItem();
            MenuItem menuItem3 = new MenuItem();
            MenuItem menuItem4 = new MenuItem();

            //menuItem.Header = "アプリケーションを登録";
            menuItem0.Header = "表示アプリ名を変更";
            menuItem1.Header = "ファイル別作業時間を確認";
            menuItem2.Header = "ファイル拡張子を設定";
            menuItem3.Header = "一覧から削除";
            menuItem4.Header = "表示内容をコピー";

            //menuItem.Click += OnClickAddApp;
            menuItem0.Click += menuItem_ChangeNameOfDesplayedName;
            menuItem1.Click += menuItem_ConfirmTimeOfFiles;
            menuItem2.Click += menuItem_SetFileExtension;
            menuItem3.Click += menuIten_Delete;
            menuItem4.Click += menuItem_Copy;

            ContextMenu contextMenu = new ContextMenu();
            //contextMenu.Items.Add(menuItem);
            contextMenu.Items.Add(menuItem0);
            contextMenu.Items.Add(menuItem4);
            contextMenu.Items.Add(menuItem1);
            contextMenu.Items.Add(menuItem2);
            contextMenu.Items.Add(menuItem3);

            listView.ContextMenu = contextMenu;

        }

        /// <summary>
        /// 右クリックメニュー>表示アプリ名を変更 をクリック時
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void menuItem_ChangeNameOfDesplayedName(object sender, RoutedEventArgs e)
        {
            AppDataObject appData = (AppDataObject)listView.SelectedItem;
            var appNameSettingWindow = new AppNameSettingWindow(this, appData);
            appNameSettingWindow.ShowDialog();
        }

        /// <summary>
        /// 右クリックメニュー>ファイル別作業時間を確認 をクリック時
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void menuItem_ConfirmTimeOfFiles(object sender, RoutedEventArgs e)
        {
            Console.WriteLine(FileListWindows);
            FileListWindows.Find((x) => x.AppData == listView.SelectedItem).Show();
        }

        /// <summary>
        /// 右クリックメニュー>ファイル拡張子を設定 をクリック時
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void menuItem_SetFileExtension(object sender, RoutedEventArgs e)
        {
            AppDataObject appData = (AppDataObject)listView.SelectedItem;
            var fileExtension = new FileExtensionSettingWindow(appData);
            fileExtension.ShowDialog();
        }

        /// <summary>
        /// 右クリックメニュー>表示内容をコピー をクリック時
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void menuItem_Copy(object sender, RoutedEventArgs e)
        {
            string text = "";
            foreach (var item in listView.SelectedItems)
            {
                AppDataObject data = (AppDataObject)item;
                text += $"{data.DisplayedName}の起動時間 今日:{data.GetTodaysTime} 累積:{data.GetTotalTime}\n";
            }

            if (text != "")
            {
                Clipboard.SetDataObject(text);
            }
        }

        /// <summary>
        /// 右クリックメニュー>削除　をクリック時
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void menuIten_Delete(object sender, RoutedEventArgs e)
        {
            if (listView.SelectedItems == null)
                return;

            string msg = "選択された項目を一覧から削除します。\nよろしいですか？\n(データは削除されます)";
            MessageBoxResult res = MessageBox.Show(msg, "削除確認", MessageBoxButton.OKCancel,
            MessageBoxImage.None, MessageBoxResult.Cancel);
            switch (res)
            {
                case MessageBoxResult.OK:
                    //var appdataObjects = listView.SelectedItems.Cast<AppDataObject>();
                    //選択された項目を削除
                    while (listView.SelectedItems.Count > 0)
                    {
                        var fileListWindow = FileListWindows.Find((x) => x.AppData == listView.SelectedItem);
                        FileListWindows.Remove(fileListWindow);
                        AppDataObject myobj;
                        myobj = (AppDataObject)listView.SelectedItems[0];
                        RemoveAppData(myobj);
                    }
                    break;
                case MessageBoxResult.Cancel:
                    // Cancelの処理
                    break;
            }
            this.UpdateListView();
        }

        /// <summary>
        /// アプリケーションのデータを削除
        /// </summary>
        /// <param name="obj"></param>
        private void RemoveAppData(AppDataObject obj)
        {
            AppDatas.Remove(obj);
            listView.Items.Remove(obj);
            listView.Items.Refresh();
            SaveCsvData();
        }

        #endregion


        #region リストビュー

        /// <summary>
        /// リストビューを初期化する
        /// </summary>
        private void InitListView()
        {
            foreach (AppDataObject appData in AppDatas)
            {
                appData.Init();
                //var iconImagePath = Directory.GetCurrentDirectory() + "/data/icons/" + $"{data.ProcessName}.png";
                //data.LoadIconImage(iconImagePath);
                //listView.Items.Add(data);
                //AddFileListWindow(data);
            }
            UpdateListView();
        }

        /// <summary>
        /// 実行ファイルのパスからアプリケーションを登録する
        /// </summary>
        /// <param name="filePath"></param>
        /// <param name="isFromDropped"></param>
        public void AddListFromPath(string filePath, bool isFromDropped = true)
        {
            string[] parsed = filePath.Split('\\');
            string name = parsed.Last().Replace(".exe", "");

            if (AppDatas.Any(a => a.ProcessName == name))
            {
                if (!isFromDropped)
                {
                    MessageBox.Show("既に登録されています");
                }
            }
            else
            {
                AppDataObject appData = new AppDataObject(this, name)
                {
                    DisplayedName = name
                };
                appData.SetIcon(filePath);
                AppDatas.Add(appData);
                //listView.Items.Add(appData);
                //AddFileListWindow(appData);
                SaveCsvData();
            }
            UpdateListView();
        }

        /// <summary>
        /// リストビューの表示を更新する
        /// </summary>
        public void UpdateListView()
        {
            //削除を先に行う
            foreach (var item in listView.Items)
            {
                //データが削除された
                if (!AppDatas.Contains(item))
                {
                    Dispatcher.BeginInvoke(new Action(() => listView.Items.Remove(item)));
                    var fileListWindow = FileListWindows.Find((x) => x.AppData == item);
                    FileListWindows.Remove(fileListWindow);
                }
            }

            foreach (var appData in AppDatas)
            {
                //新しいデータが見つかった
                if (!listView.Items.Contains(appData))
                {
                    Dispatcher.BeginInvoke(new Action(() => listView.Items.Add(appData)));
                    //listView.Items.Add(appData);
                    AddFileListWindow(appData);
                    //var fileListWindow = FileListWindows.Find((x) => x.AppData == appData);
                    //FileListWindows.Add(fileListWindow);
                }
            }
            Dispatcher.BeginInvoke(new Action(() => listView.Items.Refresh()));
        }

        /// <summary>
        /// リストビューにアイテムを追加する
        /// </summary>
        /// <param name="data"></param>
        private void AddFileListWindow(AppDataObject data)
        {
            FileViewWindow fileListWindow = new FileViewWindow(data);
            FileListWindows.Add(fileListWindow);
        }

        #endregion


        /// <summary>
        /// アプリデータを保存
        /// </summary>
        /// <param name="path">保存先パス</param>
        public void SaveCsvData(string path = "")
        {
            try
            {
                string filePath = Settings.CurrentDir + "/data/appData.csv";
                //var uri = new Uri("data/appData.csv", UriKind.Relative);
                //string csvData = uri.ToString();

                using (var sw = new StreamWriter(filePath, false, Encoding.UTF8))
                {
                    sw.WriteLine($"アプリケーション名,今日の起動時間(分),累積起動時間(分),最終起動日時," +
                        $"toggle連携フラグ,連携プロジェクト名,連携タグ名, ファイル拡張子");
                    foreach (AppDataObject appData in AppDatas)
                    {
                        sw.WriteLine($"{appData.ProcessName}," +
                            $"{appData.TodaysMinutes}," +
                            $"{appData.TotalMinutes}," +
                            $"{appData.GetLastLaunchedTime}," +
                            $"{appData.IsLinkedToToggle.ToString()}," +
                            $"{appData.LinkedProjectName}," +
                            $"{appData.LinkedTag}," +
                            $"{appData.GetFileExtensionText()}");
                    }
                }
            }
            catch (Exception ex)
            {
                //MessageBox.Show(ex.ToString());
                Console.WriteLine(ex.Message);
            }

            foreach (AppDataObject appData in AppDatas)
            {
                appData.SaveFileData();
            }
        }

        /// <summary>
        /// アプリデータを読み込み
        /// </summary>
        /// <param name="path">保存先パス</param>
        private void LoadCsvData ()
        {
            try
            {
                //Uri uri = new Uri(@"data/appData.csv", UriKind.Relative);
                //string csvData = uri.ToString();
                string filePath = Settings.CurrentDir + "/data/appData.csv";

                //using (StreamReader reader = new StreamReader(@"data/data.csv", Encoding.UTF8))
                using (StreamReader reader = new StreamReader(filePath, Encoding.UTF8))
                {
                    reader.ReadLine();
                    while (!reader.EndOfStream)
                    {
                        string line = reader.ReadLine();
                        string[] parsedLine = line.Split(',');
                        AppDataObject data = new AppDataObject(this, parsedLine[0])
                        {
                            DisplayedName = parsedLine[0],
                            TodaysMinutes = int.Parse(parsedLine[1]),
                            TotalMinutes = int.Parse(parsedLine[2]),
                            //[3]
                            IsLinkedToToggle = bool.Parse(parsedLine[4]),
                            LinkedProjectName = parsedLine[5],
                            LinkedTag = parsedLine[6]
                        };
                        data.SetLastLaunchedTime(parsedLine[3]);
                        data.SetFileExtensions(parsedLine[7]);
                        AppDatas.Add(data);
                    }
                }
            }
            catch (Exception ex)
            {
                //MessageBox.Show(ex.ToString());
                Console.WriteLine(ex.Message);
            }

            foreach (AppDataObject appData in AppDatas)
            {
                appData.LoadFileData();
            }
        }

        private void listView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }

        #region ドラッグ＆ドロップ処理

        private void Window_Drop(object sender, DragEventArgs e)
        {
            //MyFileList list = this.DataContext as MyFileList;
            string[] files = e.Data.GetData(DataFormats.FileDrop) as string[];
            string text = "";
            if (files != null)
            {
                foreach (string s in files)
                {
                    text += s + "\n";
                }
                //var file = files[0];
                
                foreach (string file in files)
                {
                    GetFilePathFromDroppedLinks(file);
                }
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

        private void GetFilePathFromDroppedLinks(string file)
        {
            string extension = System.IO.Path.GetExtension(file);
            if (".lnk" == extension)
            {
                IWshRuntimeLibrary.WshShell shell = new IWshRuntimeLibrary.WshShell();
                // ショートカットオブジェクトの取得
                IWshRuntimeLibrary.IWshShortcut shortcut = (IWshRuntimeLibrary.IWshShortcut)shell.CreateShortcut(file);
                // ショートカットのリンク先の取得
                string targetPath = shortcut.TargetPath.ToString();
                AddListFromPath(targetPath);
            }
            else if (extension == ".exe")
            {
                AddListFromPath(file);
            }
        }

        #endregion


    }

}


