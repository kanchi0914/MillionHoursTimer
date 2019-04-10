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
using Microsoft.WindowsAPICodePack.Dialogs;
using Path = System.IO.Path;
using System.Windows.Interop;

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

        public string Date { get; set; }
        public List<AppDataObject> AppDatas { get; set; } = new List<AppDataObject>();
        public List<FileViewWindow> FileListWindows { get; set; } = new List<FileViewWindow>();
        public SettingWindow SettingMenuWindow { get; set; }

        public TimeCounter timeCounter;
        public TogglManager TogglManager;

        public MainWindow()
        {
           
            //タスクバーに表示されないように
            ShowInTaskbar = false;

            InitNotifyIcon();

            InitializeComponent();

            LoadSettings();
            LoadCsvData();


            //設定画面
            timeCounter = new TimeCounter(this);
            TogglManager = new TogglManager(this);
            SettingMenuWindow = new SettingWindow(this);

            //日付の確認
            string currentDate = DateTime.Now.ToString("yyyy/MM/dd");
            if (Date != currentDate)
            {
                Date = currentDate;
                Properties.Settings.Default.date = Date;
                foreach (AppDataObject apps in AppDatas)
                {
                    apps.TodaysMinutes = 0;
                }
            }

            //アプリのリストビューを初期化
            try
            {
                InitListView();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        
            //メニューの作成
            CreateMenu();

            //右クリックメニューの作成
            CreateContextMenu();

        }

        /// <summary>
        /// タスクトレイアイコンを設定する
        /// </summary>
        private void InitNotifyIcon()
        {
            _notifyIcon = new System.Windows.Forms.NotifyIcon();
            _notifyIcon.Text = "MHTimer";
            _notifyIcon.Icon = new Icon("Resources/clockIcon.ico");
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

        private void _notifyIcon_MouseClick(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            try
            {
                if (e.Button == System.Windows.Forms.MouseButtons.Left)
                {
                    //ウィンドウを可視化
                    Visibility = System.Windows.Visibility.Visible;
                    WindowState = System.Windows.WindowState.Normal;
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
            //isClosingFromWindow = false;
            OnClickExit();
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
                Visibility = System.Windows.Visibility.Collapsed;
            }
            catch { }
        }

        private void OnClickExit(bool isFromWindow = false)
        {
            string text = "";
            text = "終了してよろしいですか？";
            MessageBoxResult res = MessageBox.Show(text, "Confirmation", MessageBoxButton.OKCancel,
                        MessageBoxImage.Question, MessageBoxResult.Cancel);
            if (res == MessageBoxResult.Cancel)
            {
                return;
            }

            SettingMenuWindow.Close();

            foreach (Window w in FileListWindows)
            {
                w.Close();
            }

            //アイコン表示を終了
            _notifyIcon.Dispose();

            //isFromTask = true;

            Application.Current.Shutdown();

            //Properties.Settings.Default.Reload();
            //Properties.Settings.Default.Save();
            //Properties.Settings.Default.Reload();

        }

        private void LoadSettings()
        {
            Date = Properties.Settings.Default.date;
            //IsCountingNotMinimized = Properties.Settings.Default.isCountingNotMinimized;
            //IsCountingOnlyActive = Properties.Settings.Default.isCountingOnlyActive;
        }

        private void InitListView()
        {
            foreach (AppDataObject data in AppDatas)
            {
                data.LoadIconImage();
                listView.Items.Add(data);
                AddFileListWindow(data);
            }
        }

        public void UpdateListView()
        {
            listView.Dispatcher.BeginInvoke(new Action(() => listView.Items.Refresh()));
        }

        private void AddFileListWindow(AppDataObject data)
        {
            FileViewWindow fileListWindow = new FileViewWindow(data);
            FileListWindows.Add(fileListWindow);
        }

        #region 右クリックメニュー

        private void CreateContextMenu()
        {
            //右クリックメニュー
            MenuItem menuItem = new MenuItem();
            MenuItem menuItem0 = new MenuItem();
            MenuItem menuItem1 = new MenuItem();
            MenuItem menuItem2 = new MenuItem();
            MenuItem menuItem3 = new MenuItem();
            menuItem.Header = "アプリケーションを登録";
            menuItem0.Header = "表示アプリ名を変更";
            menuItem1.Header = "ファイル別作業時間を確認";
            menuItem2.Header = "ファイル拡張子を設定";
            menuItem3.Header = "一覧から削除";

            menuItem.Click += OnClickAddApp;
            menuItem0.Click += OnClickedChangeNameOfDesplayedName;
            menuItem1.Click += OnClickedConfirmTimeOfFiles;
            menuItem2.Click += OnClickedSetFileExtension;
            menuItem3.Click += OnClickedDelete;

            ContextMenu contextMenu = new ContextMenu();
            contextMenu.Items.Add(menuItem);
            contextMenu.Items.Add(menuItem0);
            contextMenu.Items.Add(menuItem1);
            contextMenu.Items.Add(menuItem2);
            contextMenu.Items.Add(menuItem3);

            listView.ContextMenu = contextMenu;

        }

        private void OnClickedChangeNameOfDesplayedName(object sender, RoutedEventArgs e)
        {
            AppDataObject appData = (AppDataObject)listView.SelectedItem;
            var appNameSettingWindow = new AppNameSettingWindow(this, appData);
            appNameSettingWindow.Show();
        }

        private void OnClickedConfirmTimeOfFiles(object sender, RoutedEventArgs e)
        {
            Console.WriteLine(FileListWindows);
            FileListWindows[listView.SelectedIndex].Show();
        }

        private void OnClickedSetFileExtension(object sender, RoutedEventArgs e)
        {
            AppDataObject appData = (AppDataObject)listView.SelectedItem;
            var fileExtension = new FileExtensionSettingWindow(appData);
            fileExtension.Show();
        }

        /// <summary>
        /// 右クリックメニュー＞削除　をクリック時に呼ばれる
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnClickedDelete(object sender, RoutedEventArgs e)
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
                        AppDataObject myobj;
                        myobj = (AppDataObject)listView.SelectedItems[0];
                        RemoveAppDate(myobj);
                    }
                    break;
                case MessageBoxResult.Cancel:
                    // Cancelの処理
                    break;
            }
        }

        private void RemoveAppDate(AppDataObject obj)
        {
            AppDatas.Remove(obj);
            listView.Items.Remove(obj);
            listView.Items.Refresh();
            SaveCsvData();
        }

        #endregion

        public void CreateMenu()
        {
            AddApp.Click += OnClickAddApp;
            Import.Click += OnClickImportData;
            Export.Click += OnClickExportData;
        }


        //記録するアプリケーションの登録
        private void OnClickAddApp(object sender, RoutedEventArgs e)
        {
            string path = GetFilePathByFileDialog();

            if (!string.IsNullOrEmpty(path))
            {
                AddListFromPath(path);
            }
        }

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

        public void OnClickImportData(object sender, RoutedEventArgs e)
        {
            string path = GetFolderPathByFileDialog();
            if (!string.IsNullOrEmpty(path))
            {
                var splitted = path.Split('\\');
                if (splitted.Last() != "data")
                {
                    MessageBox.Show("dataフォルダを選択してください",
                        "エラー",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    return;
                }

                DirectoryProcessor.CopyAndReplace(@path, "data");
            }
        }

        public void OnClickExportData(object sender, RoutedEventArgs e)
        {
            string path = GetFolderPathByFileDialog();

            path += "\\data";

            if (!string.IsNullOrEmpty(path))
            {
                DirectoryProcessor.CopyAndReplace("data", @path);
            }
        }

        public void AddListFromPath(string filePath, bool isFromDropped = true)
        {
            string[] parsed = filePath.Split('\\');
            string name = parsed.Last().Replace(".exe", "");

            //重複を確認し、なければ登録
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
                listView.Items.Add(appData);
                AddFileListWindow(appData);
                SaveCsvData();
            }
        }

        public void SaveCsvData(string path = "")
        {
            try
            {
                var uri = new Uri("data/appData.csv", UriKind.Relative);
                string csvData = uri.ToString();

                using (var sw = new System.IO.StreamWriter(csvData, false, Encoding.UTF8))
                {
                    sw.WriteLine($"アプリケーション名,今日の起動時間,累積起動時間,最終起動日時,toggleと連携するか,連携プロジェクト名");
                    foreach (AppDataObject appData in AppDatas)
                    {
                        sw.WriteLine($"{appData.ProcessName},{appData.TodaysMinutes},{appData.TotalMinutes}," +
                            $"{appData.GetLastLaunchedTime}, {appData.IsLinkedToToggle.ToString()}, {appData.LinkedProjectName}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }

            foreach (AppDataObject appData in AppDatas)
            {
                appData.SaveFileData();
            }
        }


        private void LoadCsvData ()
        {
            try
            {
                Uri uri = new Uri(@"data/appData.csv", UriKind.Relative);
                string csvData = uri.ToString();

                //using (StreamReader reader = new StreamReader(@"data/data.csv", Encoding.UTF8))
                using (StreamReader reader = new StreamReader(csvData, Encoding.UTF8))
                {
                    reader.ReadLine();
                    while (!reader.EndOfStream)
                    {
                        String line = reader.ReadLine();
                        String[] parsedLine = line.Split(',');
                        AppDataObject data = new AppDataObject(this, parsedLine[0])
                        {
                            DisplayedName = parsedLine[0],
                            TodaysMinutes = int.Parse(parsedLine[1]),
                            TotalMinutes = int.Parse(parsedLine[2]),
                            //[3]
                            IsLinkedToToggle = bool.Parse(parsedLine[4]),
                            LinkedProjectName = parsedLine[5]
                        };
                        data.SetLastLaunchedTime(parsedLine[3]);
                        AppDatas.Add(data);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }

            foreach (AppDataObject appData in AppDatas)
            {
                appData.LoadFileData();
            }
        }


        private void topMenu_Click(object sender, RoutedEventArgs e)
        {
            if (!SettingMenuWindow.IsActive)
            {
                SettingMenuWindow = new SettingWindow(this);
                SettingMenuWindow.ShowDialog();
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


