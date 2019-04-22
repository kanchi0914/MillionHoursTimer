using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Configuration;

namespace WpfApp2
{
    
    /// <summary>
    /// Window2.xaml の相互作用ロジック
    /// </summary>
    public partial class SettingWindow : Window
    {
        //ウィンドウから終了したかの確認フラグ
        private bool isFromWindow = false;

        public MainWindow mainWindow;

        private TextBox apiKeyInput;
        private TextBox minCountStartTimeInput;
        private TextBox countIntervalInput;

        public class ProjectData
        {
            public string ProjectName { get; set; }
            public int ProjectID { get; set; }
        }

        public List<ProjectData> ProjectDatas { get; set; } = new List<ProjectData>();

        public SettingWindow(MainWindow mainWindow)
        {
            InitializeComponent();

            ApplicationListInToggleSetting.ItemsSource = mainWindow.AppDatas;

            this.mainWindow = mainWindow;

            //コンポーネントの初期化
            InitComponents();

            //ウィンドウを開いた時の設定
            Activated += (s, e) => SetWindowFlag();

            //終了時に呼ばれる
            Closed += (s, e) => Exit();

            //togglリストを初期化
            InitTogglList();
        }

        public void SetWindowFlag()
        {
            isFromWindow = true;
        }

        public void InitComponents()
        {
            AutoLaunch.IsChecked = Settings.IsAutoLauch;
            NotCountOnSleep.IsChecked = Settings.IsNotCountingOnSleep;

            NotCountMinimized.IsChecked = Settings.IsCountingNotMinimized;
            OnlyCountActive.IsChecked = Settings.IsCountingOnlyActive;
            AdditionalCount.IsChecked = Settings.IsEnabledAdditionalFileNameSetting;

            OKButton.AddHandler(System.Windows.Controls.Primitives.ButtonBase.ClickEvent,
                new RoutedEventHandler(OnClickedOKButton));

            CountInterval.Text = Settings.CountInterval.ToString();
            MinCountTime.Text = Settings.MinCountStartTime.ToString();
            MaxFileNum.Text = Settings.MaxFileNum.ToString();

            APIKeyInput.Text = mainWindow.TogglManager.ApiKey;
        }

        public void InitTogglList()
        {
             RefreshToggleList();
        }

        public void RefreshToggleList()
        {
            User.Text = "ユーザー：" + mainWindow.TogglManager.User;
            foreach (AppDataObject data in mainWindow.AppDatas)
            {
                data.ProjectNames = mainWindow.TogglManager.ProjectIDs.Keys.ToList();
                data.TagNames = mainWindow.TogglManager.Tags;
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
                    RefreshToggleList();
                    ApplicationListInToggleSetting.Dispatcher.BeginInvoke(new Action(() => ApplicationListInToggleSetting.Items.Refresh()));
                    MessageBox.Show("認証が完了しました");
                }
                catch (Exception ex)
                {
                    //Console.WriteLine(ex);
                    MessageBox.Show("API Keyの認証に失敗しました。正しく入力されているか確認してください");
                }
            }

        }

        private void textBoxPrice_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            // 0-9のみ
            e.Handled = !new Regex("[0-9]").IsMatch(e.Text);
        }
        private void textBoxPrice_PreviewExecuted(object sender, ExecutedRoutedEventArgs e)
        {
            // 貼り付けを許可しない
            if (e.Command == ApplicationCommands.Paste)
            {
                e.Handled = true;
            }
        }

        /// <summary>
        /// ウィンドウを閉じたときに呼ばれる
        /// </summary>
        private void Exit()
        {
            //アプリ終了時に呼ばれた場合は変更を加えない
            if (isFromWindow)
            {
                Settings.APIKey = mainWindow.TogglManager.ApiKey;

                Settings.IsAutoLauch = (bool)AutoLaunch.IsChecked;
                Settings.IsNotCountingOnSleep = (bool)NotCountOnSleep.IsChecked;

                Settings.IsCountingNotMinimized = (bool)NotCountMinimized.IsChecked;
                Settings.IsCountingOnlyActive = (bool)OnlyCountActive.IsChecked;
                Settings.IsEnabledAdditionalFileNameSetting = (bool)AdditionalCount.IsChecked;

                if (int.Parse(CountInterval.Text) > 0) Settings.CountInterval = int.Parse(CountInterval.Text);
                if (int.Parse(MinCountTime.Text) > 0) Settings.MinCountStartTime = int.Parse(MinCountTime.Text);
                if (int.Parse(MaxFileNum.Text) > 0) Settings.MaxFileNum = int.Parse(MaxFileNum.Text);

                Settings.Save();

                SetAutoLaunch(Settings.IsAutoLauch);

                mainWindow.SaveCsvData();

                mainWindow.timeCounter.UpdateTimer();
                isFromWindow = false;
            }
        }

        /// <summary>
        /// 自動起動の設定
        /// </summary>
        /// <param name="isOn"></param>
        private void SetAutoLaunch(bool isOn = true)
        {
            var Name = System.Reflection.Assembly.GetExecutingAssembly().GetName().Name;
            var path = System.Reflection.Assembly.GetExecutingAssembly().Location.ToString();
            if (isOn)
            {
                try
                {
                    Microsoft.Win32.RegistryKey regkey =
                        Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                        @"Software\Microsoft\Windows\CurrentVersion\Run", true);
                    regkey.SetValue(Name, path + " -v");
                    regkey.Close();
                }
                catch
                {
                    Console.WriteLine("");
                }
            }
            else
            {
                try
                {
                    Microsoft.Win32.RegistryKey regkey =
                        Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                        @"Software\Microsoft\Windows\CurrentVersion\Run", true);
                    regkey.DeleteValue(Name, false);
                    regkey.Close();
                }
                catch { }
            }
        }

        private void Maintab_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }
    }
}
