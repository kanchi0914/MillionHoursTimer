using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace MHTimer
{
    public class ListViewSetter
    {
        MainWindow mainWindow;

		public ListViewSetter(MainWindow mainWindow)
        {
            this.mainWindow = mainWindow;
            //アプリのリストビューを初期化
            try
            {
                InitListView();
            }
            catch (Exception ex)
            {
                MessageBox.Show("エラー:\n" + ex.ToString());
            }
        }

        /// <summary>
        /// リストビューを初期化する
        /// </summary>
        private void InitListView()
        {
            mainWindow.listView.ItemsSource = mainWindow.AppDatas;
            mainWindow.AppDatas.ToList().ForEach(a => AddFileListWindow(a));
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
            if (mainWindow.AppDatas.Any(a => a.ProcessName == name))
            {
                if (!isFromDropped)
                {
                    MessageBox.Show("既に登録されています");
                }
            }
            else
            {
                Mouse.OverrideCursor = Cursors.Wait;
                AppDataObject appData = new AppDataObject(mainWindow, name)
                {
                    DisplayedName = name
                };
                IconGetter.SetIconToNewAppData(filePath, appData);
                AddFileListWindow(appData);
                lock (mainWindow.AppDatas)
                {
                    mainWindow.AppDatas.Add(appData);
                }

                mainWindow.SaveAndLoader.SaveCsvData();
            }
            UpdateListView();
            Mouse.OverrideCursor = null;
        }

        /// <summary>
        /// リストビューの表示を更新する
        /// </summary>
        public void UpdateListView()
        {
            mainWindow.FileViewWindows.ForEach(f =>
            {
                f.Dispatcher.BeginInvoke(new Action(() => f.fileListView.Items.Refresh()));
            });

            mainWindow.Dispatcher.BeginInvoke(new Action(() =>
            {
                mainWindow.listView.Items.Refresh();
            }));
        }

        /// <summary>
        /// リストビューにアイテムを追加する
        /// </summary>
        /// <param name="data"></param>
        private void AddFileListWindow(AppDataObject data)
        {
            FileViewWindow fileListWindow = new FileViewWindow(data);
            mainWindow.FileViewWindows.Add(fileListWindow);
        }


        public void SetFilePathFromDroppedLinks(string file)
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
    }
}
