using System;

namespace CustomAction2
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
            var systemPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            var name = System.Reflection.Assembly.GetExecutingAssembly().GetName().Name;
            var dirPath = systemPath + @"\" + name;
            var dirInfo = new System.IO.DirectoryInfo(dirPath);

            //これだと削除されない？
            //MHTimer.AutoLaunchSetter.SetAutoLaunch(false);
            RemoveKey(name);

            base.Uninstall(savedState);

            //新しく作成されたファイルは削除されないので、アンインストール後に削除
            try
            {
                dirInfo.Create();
            }
            catch
            {

            }
        }

        private void RemoveKey(string Key)
        {
            try
            {
                Microsoft.Win32.RegistryKey regkey =
                    Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Run", true);
                regkey.DeleteValue(Key, false);
                regkey.Close();
            }
            catch
            {

            }
        }
    }
}