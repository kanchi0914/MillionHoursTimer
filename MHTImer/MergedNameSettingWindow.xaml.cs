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
using System.Windows.Shapes;

namespace MHTimer
{
    public partial class MergedNameSettingWindow : Window
    {
        FileViewWindow fileViewWindow;

        public MergedNameSettingWindow(FileViewWindow fileViewWindow)
        {
            InitializeComponent();
            this.fileViewWindow = fileViewWindow;
            TextBox.Text = ((FileDataObject)fileViewWindow.fileListView.SelectedItems[0]).Name;
            OKButton.AddHandler(System.Windows.Controls.Primitives.ButtonBase.ClickEvent,
                new RoutedEventHandler(okButton_OnClicked));
        }
        
        public void okButton_OnClicked(object sender, RoutedEventArgs e)
        {
            var sumTime = new TimeSpan(0, 0, 0);
            var appData = fileViewWindow.AppData;

            //var selectedItems = (List<FileDataObject>)fileViewWindow.fileListView.SelectedItems;

            //for (int i = selectedItems.Count -1; i >= 0; i--)
            //{
            //    sumTime += selectedItems[i].TotalTime;
            //    appData.RemoveFileDataFromList(selectedItems[i]);
            //}

            for (int i = appData.Files.Count - 1; i >= 0; i--)
            {
                if (fileViewWindow.fileListView.SelectedItems.Contains(appData.Files[i]))
                {
                    sumTime += appData.Files[i].TotalTime;
                    appData.RemoveFileDataFromList(appData.Files[i]);
                }
            }

            var fileData = AppDataObject.CreateFileDate(TextBox.Text, sumTime);

            //マージ後のデータを追加
            fileViewWindow.AppData.AddFileDataToList(fileData);

            ////リストビューを更新
            //fileViewWindow.UpdateListView();

            Close();
        }

    }
}
