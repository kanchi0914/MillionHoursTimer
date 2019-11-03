using System;

namespace MHTimer
{
    public static class AutoLaunchSetter
    {
        /// <summary>
        /// 自動起動の設定
        /// </summary>
        /// <param name="isOn"></param>
        public static void SetAutoLaunch(bool isOn = true)
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
                catch (Exception ex)
                {
                    ErrorLogger.Log(ex);
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
                catch (Exception ex)
                {
                    ErrorLogger.Log(ex);
                }
            }
        }
    }
}
