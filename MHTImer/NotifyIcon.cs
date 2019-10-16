using System;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Drawing;
using System.Diagnostics;
using Microsoft.Win32;
using System.IO;
using Microsoft.WindowsAPICodePack.Dialogs;
using Path = System.IO.Path;



public class NotifyIcon
{
	public NotifyIcon(MainWindow mainWindow)
	{
        Unko();
	}

    /// <summary>
    /// タスクトレイアイコンを設定する
    /// </summary>
    private void InitNotifyIcon()
    {
        _notifyIcon = new System.Windows.Forms.NotifyIcon();
        _notifyIcon.Text = "MHTimer";
        var iconFilePath = AppDomain.CurrentDomain.SetupInformation.ApplicationBase
            + "/Resources/clockIcon.ico";

        _notifyIcon.Icon = new Icon(@iconFilePath);
        _notifyIcon.Visible = true;

        System.Windows.Forms.ContextMenuStrip menuStrip = new System.Windows.Forms.ContextMenuStrip();

        System.Windows.Forms.ToolStripMenuItem exitItem = new System.Windows.Forms.ToolStripMenuItem();
        exitItem.Text = "終了";
        menuStrip.Items.Add(exitItem);
        exitItem.Click += new EventHandler(exitItem_Click);

        _notifyIcon.ContextMenuStrip = menuStrip;

        //タスクトレイアイコンのクリックイベントハンドラを登録する
        _notifyIcon.MouseClick += new System.Windows.Forms.MouseEventHandler(_notifyIcon_MouseClick);
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
                Visibility = Visibility.Visible;
                WindowState = WindowState.Normal;
                Activate();
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

        foreach (Window w in FileListWindows)
        {
            w.Close();
        }

        //アイコン表示を終了
        _notifyIcon.Dispose();

        Application.Current.Shutdown();

        Settings.Save();
    }

}
