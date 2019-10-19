using System.Linq;
using System.Windows.Controls;
using System.Windows;
using System.Collections.Generic;

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
            List<MenuItem> menuItems = new MenuItem[5].ToList();
            for (int i = 0; i < menuItems.Count; i++)
            {
                menuItems[i] = new MenuItem();
            }

            menuItems[0].Header = "表示アプリ名を変更";
            menuItems[1].Header = "ファイル別作業時間を確認";
            menuItems[2].Header = "ファイル拡張子を設定";
            menuItems[3].Header = "一覧から削除";
            menuItems[4].Header = "表示内容をコピー";

            menuItems[0].Click += menuItem_ClickChangeDesplayedName;
            menuItems[1].Click += menuItem_ClickConfirmTimeOfFiles;
            menuItems[2].Click += menuItem_ClickSetFileExtension;
            menuItems[3].Click += menuIten_ClickDelete;
            menuItems[4].Click += menuItem_ClickCopy;

            ContextMenu contextMenu = new ContextMenu();
            menuItems.ForEach(m => contextMenu.Items.Add(m));

            mainWindow.listView.ContextMenu = contextMenu;
        }

        /// <summary>
        /// 右クリックメニュー>表示アプリ名を変更 をクリック時
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void menuItem_ClickChangeDesplayedName(object sender, RoutedEventArgs e)
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
        private void menuItem_ClickConfirmTimeOfFiles(object sender, RoutedEventArgs e)
        {
            mainWindow.FileViewWindows.Find((x) => x.AppData == mainWindow.listView.SelectedItem).Show();
        }

        /// <summary>
        /// 右クリックメニュー>ファイル拡張子を設定 をクリック時
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void menuItem_ClickSetFileExtension(object sender, RoutedEventArgs e)
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
        private void menuItem_ClickCopy(object sender, RoutedEventArgs e)
        {
            string text = "";
            foreach (var item in mainWindow.listView.SelectedItems)
            {
                AppDataObject data = (AppDataObject)item;
                text += $"{data.DisplayedName}の起動時間 今日:{data.GetTodaysTimeText} 累積:{data.GetTotalTimeText}\n";
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
        private void menuIten_ClickDelete(object sender, RoutedEventArgs e)
        {
            if (mainWindow.listView.SelectedItems == null)
                return;

            string msg = "選択された項目を一覧から削除します。\nよろしいですか？\n(データは削除されます)";
            MessageBoxResult res = MessageBox.Show(msg, "削除確認", MessageBoxButton.OKCancel,
            MessageBoxImage.None, MessageBoxResult.Cancel);
            switch (res)
            {
                case MessageBoxResult.OK:
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
                    break;
            }
            mainWindow.ListViewSetter.UpdateListView();
        }
    }
}
