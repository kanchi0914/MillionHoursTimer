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

            Closing += Window_Closing;
        }

        public void Update()
        {
            //fileListView.Items.Dispatcher.BeginInvoke(new Action(() => fileListView.Items.Refresh()));
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
            menuItem0.Header = "コピー";
            menuItem1.Header = "削除";
            menuItem1.Click += menuitem_ClickDelete;

            ContextMenu contextMenu = new ContextMenu();
            contextMenu.Items.Add(menuItem0);
            contextMenu.Items.Add(menuItem1);

            fileListView.ContextMenu = contextMenu;
        }

        private void menuitem_ClickCopy(object sender, RoutedEventArgs e)
        {
            int index = fileListView.SelectedIndex;
            Clipboard.SetText($"{appData.Files[index].Name}：{appData.Files[index].GetTime}");
        }

        private void menuitem_ClickDelete(object sender, RoutedEventArgs e)
        {
            AppDataObject.FileData obj = (AppDataObject.FileData)fileListView.SelectedItem;
            appData.Files.Remove(obj);
            fileListView.Items.Remove(obj);
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
