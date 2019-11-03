using System.Windows;

namespace CustomAction
{
    [System.ComponentModel.RunInstaller(true)]
    public class UninstallAction : System.Configuration.Install.Installer
    {
        public override void Uninstall(System.Collections.IDictionary savedState)
        {
            base.Uninstall(savedState);
            MHTimer.AutoLaunchSetter.SetAutoLaunch(false);
            System.Windows.Forms.MessageBox.Show("アンインストールが完了しました");
        }
    }
}