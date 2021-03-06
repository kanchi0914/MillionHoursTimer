﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Navigation;
using System.Diagnostics;

namespace MHTimer
{
    
    public partial class SettingWindow : Window
    {
        //ウィンドウから終了したかの確認フラグ
        private bool isFromWindow = false;

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

            ApplicationListInToggleSetting.ItemsSource = mainWindow.AppDatas;

            this.mainWindow = mainWindow;

            //コンポーネントの初期化
            InitComponents();

            //ウィンドウを開いた時の設定
            Activated += (s, e) => SetWindowFlag();

            //終了時に呼ばれる
            Closed += (s, e) => Exit();

            //togglリストを初期化
            Activated += (s, e) => InitTogglList();
        }

        public void SetWindowFlag()
        {
            isFromWindow = true;
        }

        public void InitComponents()
        {
            AutoLaunch.IsChecked = Settings.IsAutoLauch;
            StopCountOnSleep.IsChecked = Settings.StopsOnSleep;

            CountNotMinimized.IsChecked = Settings.IsCountingNotMinimized;
            OnlyCountActive.IsChecked = Settings.IsCountingOnlyActive;

            AdditionalCount.IsChecked = Settings.IsEnabledAdditionalFileNameSetting;
            DividingBySpace.IsChecked = Settings.IsDividingBySpace;
            IgnoreChildsSetting.IsChecked = Settings.IsIgnoringChindWindowSettings;

            OKButton.AddHandler(System.Windows.Controls.Primitives.ButtonBase.ClickEvent,
                new RoutedEventHandler(okButton_OnClicked));

            CountInterval.Text = Settings.CountingSecondsInterval.ToString();
            MinCountTime.Text = Settings.MinCountStartTime.ToString();
            NoInputTime.Text = Settings.NoInputTime.ToString();
            MaxFileNum.Text = Settings.MaxFileNum.ToString();
            MinSendTime.Text = Settings.MinSendTime.ToString();

            APIKeyInput.Text = mainWindow.TogglManager.ApiKey;
        }

        public void InitTogglList()
        {
             RefreshToggleList();
        }

        public void RefreshToggleList()
        {
            User.Text = "ユーザー：" + mainWindow.TogglManager.User;
            foreach (AppDataObject appData in mainWindow.AppDatas)
            {
                appData.ProjectNames = mainWindow.TogglManager.ProjectIDs.Keys.ToList();
                appData.TagNames = mainWindow.TogglManager.Tags;
            }
        }

        public void okButton_OnClicked(object sender, RoutedEventArgs e)
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
                    MessageBox.Show("認証に失敗しました。API Keyが正しく入力されているか確認してください。\nエラー詳細:\n" 
                        + ex.ToString());
                    ErrorLogger.Log(ex);
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
                Settings.StopsOnSleep = (bool)StopCountOnSleep.IsChecked;

                Settings.IsCountingNotMinimized = (bool)CountNotMinimized.IsChecked;
                Settings.IsCountingOnlyActive = (bool)OnlyCountActive.IsChecked;
                Settings.IsEnabledAdditionalFileNameSetting = (bool)AdditionalCount.IsChecked;
                Settings.IsDividingBySpace = (bool)DividingBySpace.IsChecked;
                Settings.IsIgnoringChindWindowSettings = (bool)IgnoreChildsSetting.IsChecked;

                if (int.Parse(CountInterval.Text) > 0) Settings.CountingSecondsInterval = int.Parse(CountInterval.Text);
                Settings.MinCountStartTime = int.Parse(MinCountTime.Text);
                if (int.Parse(MaxFileNum.Text) > 0) Settings.MaxFileNum = int.Parse(MaxFileNum.Text);
                Settings.NoInputTime = int.Parse(NoInputTime.Text);
                Settings.MinSendTime = int.Parse(MinSendTime.Text);

                Settings.Save();
                
                AutoLaunchSetter.SetAutoLaunch(Settings.IsAutoLauch);

                mainWindow.SaveAndLoader.SaveCsvData();

                mainWindow.TimeCounter.UpdateTimer();
                isFromWindow = false;
            }
        }

        //ref https://codeday.me/jp/qa/20181222/31963.html
        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri));
            e.Handled = true;
        }

        private void Maintab_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }

        private void DividingBySpace_Checked(object sender, RoutedEventArgs e)
        {

        }
    }
}
