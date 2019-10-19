using System;
using System.Drawing;
using System.Windows;

namespace MHTimer
{
    public class NotifyIconSetter
    {
        private System.Windows.Forms.NotifyIcon notifyIcon;
        private MainWindow mainWindow;

        public NotifyIconSetter(MainWindow mainWindow)
        {
            this.mainWindow = mainWindow;
            Init();
        }

        /// <summary>
        /// タスクトレイアイコンを設定する
        /// </summary>
        private void Init()
        {
            notifyIcon = new System.Windows.Forms.NotifyIcon();
            notifyIcon.Text = "MHTimer";
            var iconFilePath = AppDomain.CurrentDomain.SetupInformation.ApplicationBase
                + "/Resources/clockIcon.ico";
            notifyIcon.Icon = new Icon(@iconFilePath);
            notifyIcon.Visible = true;

            System.Windows.Forms.ContextMenuStrip menuStrip = new System.Windows.Forms.ContextMenuStrip();

            System.Windows.Forms.ToolStripMenuItem exitItem = new System.Windows.Forms.ToolStripMenuItem();
            exitItem.Text = "終了";
            menuStrip.Items.Add(exitItem);
            exitItem.Click += new EventHandler(exitItem_Click);

            notifyIcon.ContextMenuStrip = menuStrip;

            //タスクトレイアイコンのクリックイベントハンドラを登録する
            notifyIcon.MouseClick += new System.Windows.Forms.MouseEventHandler(notifyIcon_MouseClick);
        }

        /// <summary>
        /// タスクトレイアイコンをクリック時に呼ばれる
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void notifyIcon_MouseClick(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            try
            {
                if (e.Button == System.Windows.Forms.MouseButtons.Left)
                {
                    //ウィンドウを可視化
                    mainWindow.Visibility = Visibility.Visible;
                    mainWindow.WindowState = WindowState.Normal;
                    mainWindow.Activate();
                }
            }
            catch { }
        }

        /// <summary>
        /// アイコンの右クリックメニュー『終了』選択時に呼ばれる
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void exitItem_Click(object sender, EventArgs e)
        {
            //OnClickExit();
            string text = "";
            text = "終了してよろしいですか？";
            MessageBoxResult res = MessageBox.Show(text, "Confirmation", MessageBoxButton.OKCancel,
                        MessageBoxImage.Question, MessageBoxResult.Cancel);
            if (res == MessageBoxResult.Cancel)
            {
                return;
            }

            foreach (Window w in mainWindow.FileViewWindows)
            {
                w.Close();
            }

            //アイコン表示を終了
            notifyIcon.Dispose();

            Application.Current.Shutdown();

            Settings.Save();
        }
    }
}
