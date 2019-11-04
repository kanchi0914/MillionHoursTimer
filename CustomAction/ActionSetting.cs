using System.Windows;

namespace CustomAction
{
    [System.ComponentModel.RunInstaller(true)]
    public class ActionSetting : System.Configuration.Install.Installer
    {
        public override void Install(System.Collections.IDictionary savedState)
        {
            base.Install(savedState);
            MHTimer.Settings.LoadDefaultSettings();
        }

        public override void Uninstall(System.Collections.IDictionary savedState)
        {
            MHTimer.AutoLaunchSetter.SetAutoLaunch(false);
            base.Uninstall(savedState);
        }
    }
}