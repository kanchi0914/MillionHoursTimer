﻿using System;
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
            Properties.Settings.Default.Reload();
            InitializeComponent();

            ApplicationListInToggleSetting.ItemsSource = mainWindow.AppDatas;

            this.mainWindow = mainWindow;

            InitComponents();

            //終了時に呼ばれるコールバックメソッド
            Closed += (s, e) => Save();


            InitTogglList();
            //Closing += (s, e) => Closing();
        }

        public void InitComponents()
        {
            NotCountMinimized.IsChecked = Properties.Settings.Default.isCountingNotMinimized;
            OnlyCountActive.IsChecked = Properties.Settings.Default.isCountingOnlyActive;
            AdditionalCount.IsChecked = Properties.Settings.Default.isAdditionalFileName;

            OKButton.AddHandler(System.Windows.Controls.Primitives.ButtonBase.ClickEvent,
                new RoutedEventHandler(OnClickedOKButton));

            apiKeyInput = FindName("APIKeyInput") as TextBox;
            apiKeyInput.Text = mainWindow.TogglManager.ApiKey;

            minCountStartTimeInput = FindName("MinCountTime") as TextBox;
            minCountStartTimeInput.Text = Properties.Settings.Default.MinCountStartTime.ToString();

            APIKeyInput.Text = mainWindow.TogglManager.ApiKey;

            //CountInterval.Text = Properties.Settings.Default.CountInterval.ToString();
            //MinCountTime.Text = Properties.Settings.Default.MinCountStartTime.ToString();
            //MaxFileNum.Text = Properties.Settings.Default.MaxFileNum.ToString();

            //MinCountTime.Text = "1000";

            countIntervalInput = FindName("CountInterval") as TextBox;
            countIntervalInput.Text = Properties.Settings.Default.CountInterval.ToString();


        }

        public void InitTogglList()
        {
            RefreshToggleList();
        }

        public void RefreshToggleList()
        {
            User.Text = "ユーザー：" + mainWindow.TogglManager.User;

            //var projectNames = new List<string>();

            //foreach ((string s, int i) in mainWindow.togglManager.ProjectIDs)
            //{
            //    projectNames.Add(s);
            //}

            //foreach (KeyValuePair<string, int> kvp in mainWindow.TogglManager.ProjectIDs)
            //{
            //    projectNames.Add(kvp.Key);
            //}


            foreach (AppDataObject data in mainWindow.AppDatas)
            {
                //data.ProjectNames = projectNames;
                data.ProjectNames = mainWindow.TogglManager.ProjectIDs.Keys.ToList();
                data.TagNames = mainWindow.TogglManager.Tags;
            }

            //foreach ((string s, int i) in mainWindow.togglManager.ProjectIDs)
            //{
            //    var data = new ProjectData() { ProjectName = s, ProjectID = i };
            //    ProjectDatas.Add(data);
            //}

            //foreach (KeyValuePair<string, int> kvp in mainWindow.TogglManager.ProjectIDs)
            //{
            //    var data = new ProjectData() { ProjectName = kvp.Key, ProjectID = kvp.Value };
            //    ProjectDatas.Add(data);
            //}
        }

        public void OnClickedOKButton(object sender, RoutedEventArgs e)
        {
            var inputBox = FindName("APIKeyInput") as TextBox;
            if (!string.IsNullOrEmpty(inputBox.Text))
            {
                try
                {
                    mainWindow.TogglManager.SetAPIKey(inputBox.Text);
                    //InitTogglList();
                    RefreshToggleList();
                    ApplicationListInToggleSetting.Dispatcher.BeginInvoke(new Action(() => ApplicationListInToggleSetting.Items.Refresh()));
                    MessageBox.Show("認証が完了しました");
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                    MessageBox.Show(ex.ToString());
                    //MessageBox.Show("API Keyの認証に失敗しました。正しく入力されているか確認してください");
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
        private void Save()
        {
            //mainWindow.IsCountingNotMinimized = (bool)NotCountMinimized.IsChecked;
            //mainWindow.IsCountingOnlyActive = (bool)OnlyCountActive.IsChecked;

            Properties.Settings.Default.isCountingNotMinimized = (bool)NotCountMinimized.IsChecked;
            Properties.Settings.Default.isCountingOnlyActive = (bool)OnlyCountActive.IsChecked;
            Properties.Settings.Default.isAdditionalFileName = (bool)AdditionalCount.IsChecked;

            Properties.Settings.Default.CountInterval = int.Parse(countIntervalInput.Text);
            Properties.Settings.Default.MinCountStartTime = int.Parse(minCountStartTimeInput.Text);
            //Properties.Settings.Default.MaxFileNum = int.Parse(MaxFileNum.Text);

            Properties.Settings.Default.APIKey = mainWindow.TogglManager.ApiKey;

            //Properties.Settings.Default.Upgrade();
            Properties.Settings.Default.Save();

            mainWindow.timeCounter.UpdateTimer();

        }

    }
}
