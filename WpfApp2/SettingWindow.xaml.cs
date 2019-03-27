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
    /// Window2.xaml の相互作用ロジック
    /// </summary>
    public partial class SettingWindow : Window
    {

        public MainWindow mainWindow;

        public class ProjectData
        {
            public string ProjectName { get; set; }
            public int ProjectID { get; set; }
        }

        public List<ProjectData> ProjectDatas { get; set; } = new List<ProjectData>();

        public SettingWindow(MainWindow mainWindow)
        {
            InitializeComponent();

            this.mainWindow = mainWindow;

            InitComponents();


            Closed += (s, e) => Save();



            InitTogglList();
            //Closing += (s, e) => Closing();
        }

        public void InitComponents()
        {
            NotCountMinimized.IsChecked = Properties.Settings.Default.isCountingMinimized;
            OnlyCountActive.IsChecked = Properties.Settings.Default.isCountingOnlyActive;
            AdditionalCount.IsChecked = Properties.Settings.Default.isAdditionalFileName;

            OKButton.AddHandler(System.Windows.Controls.Primitives.ButtonBase.ClickEvent,
                new RoutedEventHandler(OnClickedOKButton));

            var inputBox = FindName("APIKeyInput") as TextBox;
            inputBox.Text = mainWindow.TogglManager.ApiKey;
        }

        public void InitTogglList()
        {
            ApplicationListInToggleSetting.ItemsSource = mainWindow.AppDatas;
            User.Text = "ユーザー：" + mainWindow.TogglManager.User;

            var projectNames = new List<string>();

            //foreach ((string s, int i) in mainWindow.togglManager.ProjectIDs)
            //{
            //    projectNames.Add(s);
            //}

            foreach (KeyValuePair<string, int> kvp in mainWindow.TogglManager.ProjectIDs)
            {
                projectNames.Add(kvp.Key);
            }


            foreach (AppDataObject data in mainWindow.AppDatas)
            {
                data.ProjectNames = projectNames;
                data.TagNames = mainWindow.TogglManager.Tags;
            }

            //foreach ((string s, int i) in mainWindow.togglManager.ProjectIDs)
            //{
            //    var data = new ProjectData() { ProjectName = s, ProjectID = i };
            //    ProjectDatas.Add(data);
            //}

            foreach (KeyValuePair<string, int> kvp in mainWindow.TogglManager.ProjectIDs)
            {
                var data = new ProjectData() { ProjectName = kvp.Key, ProjectID = kvp.Value };
                ProjectDatas.Add(data);
            }

        }

        public void OnClickedOKButton(object sender, RoutedEventArgs e)
        {
            var inputBox = FindName("APIKeyInput") as TextBox;
            if (!string.IsNullOrEmpty(inputBox.Text))
            {
                try
                {
                    mainWindow.TogglManager.SetAPIKey(inputBox.Text);
                    InitTogglList();
                    ApplicationListInToggleSetting.Dispatcher.BeginInvoke(new Action(() => ApplicationListInToggleSetting.Items.Refresh()));
                    MessageBox.Show("認証が完了しました");
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                    MessageBox.Show("API Keyの認証に失敗しました。正しく入力されているか確認してください");
                }
            }

        }

        private void Save()
        {
            mainWindow.IsCountingMinimized = (bool)NotCountMinimized.IsChecked;
            mainWindow.IsCountingOnlyActive = (bool)OnlyCountActive.IsChecked;

            Properties.Settings.Default.isCountingMinimized = (bool)NotCountMinimized.IsChecked;
            Properties.Settings.Default.isCountingOnlyActive = (bool)OnlyCountActive.IsChecked;
            Properties.Settings.Default.isAdditionalFileName = (bool)AdditionalCount.IsChecked;

            //Properties.Settings.Default.APIKey = mainWindow.TogglManager.ApiKey;

            Properties.Settings.Default.Save();

            Console.WriteLine();
        }

    }
}
