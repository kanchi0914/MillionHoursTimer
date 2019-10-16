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
    /// <summary>
    /// Window1.xaml の相互作用ロジック
    /// </summary>
    public partial class AppNameSettingWindow : Window
    {

        MainWindow mainWindow;
        AppDataObject appData;

        public AppNameSettingWindow(MainWindow mainWindow, AppDataObject data)
        {
            InitializeComponent();
            this.mainWindow = mainWindow;
            appData = data;
            AppNameInput.Text = appData.DisplayedName;

            OKButton.AddHandler(System.Windows.Controls.Primitives.ButtonBase.ClickEvent,
                new RoutedEventHandler(OnClickedOK));

            cancelButton.AddHandler(System.Windows.Controls.Primitives.ButtonBase.ClickEvent,
                new RoutedEventHandler(OnClickedCancel));

        }

        public void OnClickedOK(object sender, RoutedEventArgs e)
        {
            appData.DisplayedName = AppNameInput.Text;
            mainWindow.ListViewSetter.UpdateListView();
            Close();
        }

        public void OnClickedCancel(object sender, RoutedEventArgs e)
        {
            Close();
        }

    }
}
