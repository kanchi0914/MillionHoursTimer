using System;
using System.Collections.Generic;
using System.IO;
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
using System.Windows.Shapes;

namespace MHTimer
{
    /// <summary>
    /// Window1.xaml の相互作用ロジック
    /// </summary>
    public partial class FileViewWindow : Window
    {

        public AppDataObject AppData { get; }
        ContextMenu contextMenu = new ContextMenu();

        MenuItem menuItem0 = new MenuItem();
        MenuItem menuItem1 = new MenuItem();
        MenuItem menuItem2 = new MenuItem();

        public FileViewWindow(AppDataObject data)
        {
            InitializeComponent();
            this.Title = data.DisplayedName + "のファイル別作業時間一覧";
            this.AppData = data;
            foreach (AppDataObject.FileData file in AppData.Files)
            {
                fileListView.Items.Add(file);
            }

            DeleteAllButton.AddHandler(System.Windows.Controls.Primitives.ButtonBase.ClickEvent,
                new RoutedEventHandler(Button_ClickAllDelete));

            CreateMenu();

            Closing += Window_Closing;
        }

        /// <summary>
        /// リストビューを更新
        /// </summary>
        public void UpdateListView()
        {
            //削除を先に行う
            foreach (var item in fileListView.Items)
            {
                //ファイルデータが削除された
                if (!AppData.Files.Contains(item))
                {
                    Dispatcher.BeginInvoke(new Action(() => fileListView.Items.Remove(item)));
                }
            }

            foreach (AppDataObject.FileData file in AppData.Files)
            {
                //新しいファイルが見つかった
                if (!fileListView.Items.Contains(file))
                {
                    Dispatcher.BeginInvoke(new Action(() => fileListView.Items.Add(file)));
                }
            }
            Dispatcher.BeginInvoke(new Action(()=> fileListView.Items.Refresh()));
        }

        private void CreateMenu()
        {

            menuItem0.Header = "選択項目をマージ";
            menuItem0.Click += menuItem_ClickMergeItems;
            menuItem1.Header = "表示内容をコピー";
            menuItem1.Click += menuitem_ClickCopy;
            menuItem2.Header = "一覧から削除";
            menuItem2.Click += menuitem_ClickDelete;

            fileListView.ContextMenuOpening += contextMenu_Click;

            //contextMenu.ContextMenuOpening += contextMenu_Click;
            contextMenu.Items.Add(menuItem0);
            contextMenu.Items.Add(menuItem1);
            contextMenu.Items.Add(menuItem2);

            fileListView.ContextMenu = contextMenu;
        }

        /// <summary>
        /// 右クリックメニュー表示前の処理
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void contextMenu_Click(object sender, RoutedEventArgs e)
        {
            //複数選択されている場合
            menuItem0.IsEnabled = fileListView.SelectedItems.Count > 1;
        }

        private void menuItem_ClickMergeItems(object sender, RoutedEventArgs e)
        {
            var mergedNameSettingWindow = new MergedNameSettingWindow(this);
            mergedNameSettingWindow.ShowDialog();
        }

        /// <summary>
        /// 右クリック>表示内容をコピー　をクリック時
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void menuitem_ClickCopy(object sender, RoutedEventArgs e)
        {
            string text = "";
            foreach (var item in fileListView.SelectedItems)
            {
                AppDataObject.FileData fileData = (AppDataObject.FileData)item;
                text += $"{fileData.Name}の作業時間 {fileData.GetTime}\n";
            }

            if (text != "")
            {
                Clipboard.SetDataObject(text);
            }
        }


        /// <summary>
        /// 右クリックメニュー＞削除　をクリック時
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void menuitem_ClickDelete(object sender, RoutedEventArgs e)
        {
            if (fileListView.SelectedItems == null)
                return;

            string msg = "選択された項目を一覧から削除します。\nよろしいですか？\n(データは削除されます)";
            MessageBoxResult res = MessageBox.Show(msg, "削除確認", MessageBoxButton.OKCancel,
            MessageBoxImage.None, MessageBoxResult.Cancel);
            switch (res)
            {
                case MessageBoxResult.OK:
                    while (fileListView.SelectedItems.Count > 0)
                    {
                        AppDataObject.FileData data = (AppDataObject.FileData)fileListView.SelectedItems[0];
                        RemoveFileData(data);
                    }
                    break;
                case MessageBoxResult.Cancel:
                    // Cancelの処理
                    break;
            }
            UpdateListView();
        }

        /// <summary>
        /// ファイルデータを全て削除
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Button_ClickAllDelete(object sender, RoutedEventArgs e)
        {
            string msg = "記録されているデータを全て削除します。\nよろしいですか？";
            MessageBoxResult res = MessageBox.Show(msg, "削除確認", MessageBoxButton.OKCancel,
            MessageBoxImage.None, MessageBoxResult.Cancel);
            switch (res)
            {
                case MessageBoxResult.OK:
                    fileListView.SelectAll();
                    while (fileListView.SelectedItems.Count > 0)
                    {
                        AppDataObject.FileData data = (AppDataObject.FileData)fileListView.SelectedItems[0];
                        RemoveFileData(data);
                    }
                    break;
                case MessageBoxResult.Cancel:
                    // Cancelの処理
                    break;
            }
            UpdateListView();
        }

        /// <summary>
        /// ファイルデータを削除し、リストビューを更新
        /// </summary>
        /// <param name="fileData"></param>
        public void RemoveFileData(AppDataObject.FileData fileData)
        {
            AppData.RemoveFileData(fileData);
            fileListView.Items.Remove(fileData);
            fileListView.Items.Refresh();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            e.Cancel = true;
            this.Visibility = Visibility.Collapsed;
        }

    }

}
