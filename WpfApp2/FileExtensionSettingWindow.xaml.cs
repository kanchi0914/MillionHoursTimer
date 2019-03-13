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
    /// <summary>
    /// FileExtensionSettingWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class FileExtensionSettingWindow : Window
    {
        AppDataObject appData;

        public FileExtensionSettingWindow(AppDataObject appData)
        {
            this.appData = appData;
            InitializeComponent();
            TextBox.Text = string.Join("/", appData.FileExtensions.ToArray());
            OKButton.AddHandler(System.Windows.Controls.Primitives.ButtonBase.ClickEvent,
                new RoutedEventHandler(OnClicked));
        }
        
        public void OnClicked(object sender, RoutedEventArgs e)
        {
            appData.SetFileExtensions(TextBox.Text);
            Close();
        }

    }
}
