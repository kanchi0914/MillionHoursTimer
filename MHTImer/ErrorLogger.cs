using System;
using System.IO;
using System.Linq;
using System.Text;

namespace MHTimer
{
    public static class ErrorLogger
    {
        
        static readonly int maxLogFileNum = 20;

        public static void Log(Exception ex)
        {
            SafeCreateDirectory(Settings.LogDirFullPath);
            using (var sw = new StreamWriter(
                $@"{Settings.LogDirFullPath}log_{DateTime.Now.ToString("yyyyMMddHHmmss")}.txt", false, Encoding.UTF8))
            {
                sw.WriteLine(ex.ToString());
                sw.WriteLine();
            }
            RemoveOldErrorLog();
        }

        private static void RemoveOldErrorLog()
        {
            var dirInfo = new DirectoryInfo(Settings.LogDirFullPath);
            var files = dirInfo.GetFiles().ToList();
            files.OrderBy(f => f.LastWriteTime);
            if (files.Count > maxLogFileNum)
            {
                for (int i = (files.Count - maxLogFileNum) - 1; i > -1; i--)
                {
                    files[i].Delete();
                }
            }
        }

        public static DirectoryInfo SafeCreateDirectory(string path)
        {
            if (Directory.Exists(path))
            {
                return null;
            }
            return Directory.CreateDirectory(path);
        }
    }
}
