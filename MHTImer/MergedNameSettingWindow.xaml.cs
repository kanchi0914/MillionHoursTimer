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
            TextBox.Text = ((AppDataObject.FileData)fileViewWindow.fileListView.SelectedItems[0]).Name;
            OKButton.AddHandler(System.Windows.Controls.Primitives.ButtonBase.ClickEvent,
                new RoutedEventHandler(OnClicked));
        }
        
        public void OnClicked(object sender, RoutedEventArgs e)
        {
            int sumTime = 0;
            var appData = fileViewWindow.AppData;

            for (int i = appData.Files.Count - 1; i >= 0; i--)
            {
                if (fileViewWindow.fileListView.SelectedItems.Contains(appData.Files[i]))
                {
                    sumTime += appData.Files[i].TotalMinutes;
                    appData.RemoveFileData(appData.Files[i]);
                }
            }

            //foreach (AppDataObject.FileData data in fileViewWindow.AppData.Files)
            //{
            //    sumTime += data.TotalMinutes;
            //    fileViewWindow.AppData.RemoveFileData(data);
            //}

            //while (fileViewWindow.AppData.Files.Count > 0)
            //{
            //    AppDataObject myobj;
            //    myobj = (AppDataObject)listView.SelectedItems[0];
            //    RemoveAppData(myobj);
            //}

            //マージ後のデータを追加
            fileViewWindow.AppData.AddFileData(TextBox.Text, 9999);

            //リストビューを更新
            fileViewWindow.UpdateListView();

            Close();
        }

    }
}
