using System;
using System.Windows;
using System.Windows.Controls;

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

            for (int i = appData.Files.Count - 1; i >= 0; i--)
            {
                if (fileViewWindow.fileListView.SelectedItems.Contains(appData.Files[i]))
                {
                    sumTime += appData.Files[i].TotalTime;
                    appData.RemoveFileDataFromList(appData.Files[i]);
                }
            }

            var fileData = AppDataObject.CreateFileDate(TextBox.Text, sumTime);
            fileViewWindow.AppData.AddFileDataToList(fileData);

            Close();
        }

    }
}
