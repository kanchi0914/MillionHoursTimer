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

namespace WpfApp2
{
    /// <summary>
    /// Window1.xaml の相互作用ロジック
    /// </summary>
    public partial class FileViewWindow : Window
    {

        AppDataObject appData;

        public FileViewWindow(AppDataObject data)
        {
            InitializeComponent();

            this.appData = data;
            foreach (AppDataObject.FileData file in appData.Files)
            {
                fileListView.Items.Add(file);
            }

            DeleteAllButton.AddHandler(System.Windows.Controls.Primitives.ButtonBase.ClickEvent,
                new RoutedEventHandler(Button_ClickAllDelete));

            CreateMenu();

            Closing += Window_Closing;
        }

        public void Update()
        {
            foreach (AppDataObject.FileData file in appData.Files)
            {
                if (!fileListView.Items.Contains(file))
                {
                    Dispatcher.BeginInvoke(new Action(()=> fileListView.Items.Add(file)));
                }
            }
            Dispatcher.BeginInvoke(new Action(()=> fileListView.Items.Refresh()));
        }

        private void CreateMenu()
        {
            MenuItem menuItem0 = new MenuItem();
            MenuItem menuItem1 = new MenuItem();
            menuItem0.Header = "表示内容をコピー";
            menuItem0.Click += menuitem_ClickCopy;
            menuItem1.Header = "一覧から削除";
            menuItem1.Click += menuitem_ClickDelete;

            ContextMenu contextMenu = new ContextMenu();
            contextMenu.Items.Add(menuItem0);
            contextMenu.Items.Add(menuItem1);

            fileListView.ContextMenu = contextMenu;
        }

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

        //private void menuitem_ClickDelete(object sender, RoutedEventArgs e)
        //{
        //    AppDataObject.FileData obj = (AppDataObject.FileData)fileListView.SelectedItem;
        //    appData.Files.Remove(obj);
        //    fileListView.Items.Remove(obj);
        //    fileListView.Items.Refresh();
        //    appData.SaveFileData();
        //}

        /// <summary>
        /// 右クリックメニュー＞削除　をクリック時に呼ばれる
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
                        RemoveData(data);
                    }
                    break;
                case MessageBoxResult.Cancel:
                    // Cancelの処理
                    break;
            }
        }

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
                        RemoveData(data);
                    }
                    break;
                case MessageBoxResult.Cancel:
                    // Cancelの処理
                    break;
            }
        }

        private void RemoveData(AppDataObject.FileData data)
        {
            appData.Files.Remove(data);
            fileListView.Items.Remove(data);
            fileListView.Items.Refresh();
            appData.SaveFileData();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            e.Cancel = true;
            this.Visibility = Visibility.Collapsed;
        }

        protected virtual void window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (MessageBoxResult.Yes != MessageBox.Show("終了してよろしいですか？", "終了確認", MessageBoxButton.YesNo, MessageBoxImage.Information))
            {
                e.Cancel = true;
                return;
            }
        }

    }

}
