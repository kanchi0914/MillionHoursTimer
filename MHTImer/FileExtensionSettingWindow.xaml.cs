using System.Windows;
using System.Windows.Controls;

namespace MHTimer
{
    /// <summary>
    /// FileExtensionSettingWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class FileExtensionSettingWindow : Window
    {
        public AppDataObject AppData { get; }

        public FileExtensionSettingWindow(AppDataObject appData)
        {
            this.AppData = appData;
            InitializeComponent();
            TextBox.Text = appData.GetFileExtensionText();
            OKButton.AddHandler(System.Windows.Controls.Primitives.ButtonBase.ClickEvent,
                new RoutedEventHandler(OnClicked));
        }
        
        public void OnClicked(object sender, RoutedEventArgs e)
        {
            AppData.SetFileExtensions(TextBox.Text);
            Close();
        }

    }
}
