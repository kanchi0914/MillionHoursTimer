using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;

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

namespace MHTimer
{
    public class ContextMenuSetter
    {

        MainWindow mainWindow;

        public ContextMenuSetter(MainWindow mainWindow)
        {
            this.mainWindow = mainWindow;
            CreateContextMenu();
        }


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

            mainWindow.listView.ContextMenu = contextMenu;

        }

        /// <summary>
        /// 右クリックメニュー>表示アプリ名を変更 をクリック時
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void menuItem_ChangeNameOfDesplayedName(object sender, RoutedEventArgs e)
        {
            AppDataObject appData = (AppDataObject)mainWindow.listView.SelectedItem; 
            var appNameSettingWindow = new AppNameSettingWindow(mainWindow, appData);
            appNameSettingWindow.ShowDialog();
        }

        /// <summary>
        /// 右クリックメニュー>ファイル別作業時間を確認 をクリック時
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void menuItem_ConfirmTimeOfFiles(object sender, RoutedEventArgs e)
        {
            mainWindow.FileViewWindows.Find((x) => x.AppData == mainWindow.listView.SelectedItem).Show();
        }

        /// <summary>
        /// 右クリックメニュー>ファイル拡張子を設定 をクリック時
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void menuItem_SetFileExtension(object sender, RoutedEventArgs e)
        {
            AppDataObject appData = (AppDataObject)mainWindow.listView.SelectedItem;
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
            foreach (var item in mainWindow.listView.SelectedItems)
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
            if (mainWindow.listView.SelectedItems == null)
                return;

            string msg = "選択された項目を一覧から削除します。\nよろしいですか？\n(データは削除されます)";
            MessageBoxResult res = MessageBox.Show(msg, "削除確認", MessageBoxButton.OKCancel,
            MessageBoxImage.None, MessageBoxResult.Cancel);
            switch (res)
            {
                case MessageBoxResult.OK:
                    //var appdataObjects = listView.SelectedItems.Cast<AppDataObject>();
                    //選択された項目を削除
                    while (mainWindow.listView.SelectedItems.Count > 0)
                    {
                        var fileListWindow = mainWindow.FileViewWindows.Find((x) => x.AppData == mainWindow.listView.SelectedItem);
                        mainWindow.FileViewWindows.Remove(fileListWindow);
                        AppDataObject myobj;
                        myobj = (AppDataObject)mainWindow.listView.SelectedItems[0];
                        mainWindow.RemoveAppData(myobj);
                    }
                    break;
                case MessageBoxResult.Cancel:
                    // Cancelの処理
                    break;
            }
            mainWindow.ListViewSetter.UpdateListView();
        }


    }
}
