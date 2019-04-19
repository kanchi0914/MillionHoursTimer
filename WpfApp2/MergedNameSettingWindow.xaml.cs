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

namespace WpfApp2
{
    public partial class MergedNameSettingWindow : Window
    {
        AppDataObject appData;
        List<AppDataObject.FileData> fileDatas;

        public MergedNameSettingWindow(AppDataObject appData, List<AppDataObject.FileData> fileDatas)
        {
            InitializeComponent();
            this.appData = appData;
            this.fileDatas = fileDatas;
            TextBox.Text = fileDatas[0].Name;
            OKButton.AddHandler(System.Windows.Controls.Primitives.ButtonBase.ClickEvent,
                new RoutedEventHandler(OnClicked));
        }
        
        public void OnClicked(object sender, RoutedEventArgs e)
        {
            int sumTime = 0;
            foreach (AppDataObject.FileData data in fileDatas)
            {
                sumTime += data.TotalMinutes;
                appData.Files.Remove(data);
            }

            appData.AddFileData(TextBox.Text, sumTime);
            Close();
        }

    }
}
