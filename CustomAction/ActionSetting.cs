using System;
namespace CustomAction
{
    [System.ComponentModel.RunInstaller(true)]
    public class ActionSetting : System.Configuration.Install.Installer
    {
        public override void Install(System.Collections.IDictionary savedState)
        {
            base.Install(savedState);
            var systemPath = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            var parentDirPath = systemPath + @"\" + MHTimer.Settings.ProductName;
            MHTimer.Settings.LoadDefaultSettings();
            MHTimer.Settings.AddAccessRules(parentDirPath);
        }

        public override void Uninstall(System.Collections.IDictionary savedState)
        {
            base.Uninstall(savedState);
            Remover.RemoveKey();
            DeleteDateDir();
        }

        private void DeleteDateDir()
        {

            var systemPath = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            var parentDirPath = systemPath + @"\" + MHTimer.Settings.ProductName;
            var dataDirPath = parentDirPath + MHTimer.Settings.DataDirRelativePath;
            var dataDirInfo = new System.IO.DirectoryInfo(dataDirPath);
            var logDirPath = parentDirPath + MHTimer.Settings.LogDirRelativePath;
            var logDirInfo = new System.IO.DirectoryInfo(logDirPath);
            try
            {
                if (!string.IsNullOrEmpty(MHTimer.Settings.ProductName))
                {
                    dataDirInfo.Delete(true);
                    logDirInfo.Delete(true);
                }
            }
            catch (Exception ex)
            {
                System.Windows.Forms.MessageBox.Show("dataフォルダを削除中にエラーが発生しました:" + ex.ToString());
            }
        }

        //public void RemoveKey(string key)
        //{
        //    try
        //    {
        //        Process p = new Process();
        //        p.StartInfo.FileName = "deleteRegKey.bat";
        //        p.StartInfo.Verb = "";
        //        p.StartInfo.CreateNoWindow = true;
        //        p.StartInfo.UseShellExecute = false;
        //        p.StartInfo.WorkingDirectory = MHTimer.Settings.CurrentDir;

        //    }
        //    catch (Exception ex)
        //    {
        //        throw ex;
        //    }
        //}
    }
}