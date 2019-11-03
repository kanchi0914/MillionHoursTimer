using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;

namespace MHTimer
{
    public class SaveAndLoader
    {

        MainWindow mainWindow;

        public SaveAndLoader(MainWindow mainWindow)
        {
            this.mainWindow = mainWindow;
        }

        /// <summary>
        /// アプリデータを保存
        /// </summary>
        /// <param name="path">保存先パス</param>
        public void SaveCsvData(string path = "")
        {
            try
            {
                string filePath = Settings.CurrentDir + "/data/appData.csv";

                using (var sw = new StreamWriter(filePath, false, Encoding.UTF8))
                {
                    sw.WriteLine($"アプリケーション名,今日の起動時間,累積起動時間,最終起動日時," +
                        $"toggle連携フラグ,連携プロジェクト名,連携タグ名, ファイル拡張子");
                    foreach (AppDataObject appData in mainWindow.AppDatas)
                    {
                        sw.WriteLine($"{appData.ProcessName}," +
                            $"{AppDataObject.ConvertTimeSpanToSavingFormattedString(appData.TodaysTime)}," +
                            $"{AppDataObject.ConvertTimeSpanToSavingFormattedString(appData.TotalTime)}," +
                            $"{appData.GetLastLaunchedTimeText}," +
                            $"{appData.IsLinkedToToggl.ToString()}," +
                            $"{appData.LinkedProjectName}," +
                            $"{appData.LinkedTag}," +
                            $"{appData.GetFileExtensionText()}");
                    }
                }
            }
            catch (Exception ex)
            {
                ErrorLogger.Log(ex);
            }

            mainWindow.AppDatas.ToList().ForEach(a => a.SaveFileDatas());

        }

        /// <summary>
        /// アプリデータを読み込み
        /// </summary>
        /// <param name="path">保存先パス</param>
        public void LoadData()
        {
            try
            {
                string filePath = Settings.CurrentDir + "/data/appData.csv";
                using (StreamReader reader = new StreamReader(filePath, Encoding.UTF8))
                {
                    reader.ReadLine();
                    while (!reader.EndOfStream)
                    {
                        string line = reader.ReadLine();
                        string[] parsedLine = line.Split(',');
                        AppDataObject data = new AppDataObject(mainWindow, parsedLine[0]);
                        data.DisplayedName = parsedLine[0];
                        data.TodaysTime = AppDataObject.ConvertStringToTimeSpan(parsedLine[1]);
                        data.TotalTime = AppDataObject.ConvertStringToTimeSpan(parsedLine[2]);

                        //structなので元データはnullでよい
                        data.SetLastRunningTime(parsedLine[3]);
                        
                        //nullの場合はfalse
                        bool isLinkedToToggl;
                        bool.TryParse(parsedLine[4], out isLinkedToToggl);
                        data.IsLinkedToToggl = isLinkedToToggl;
                        
                        //nullでよい
                        data.LinkedProjectName = parsedLine[5];
                        data.LinkedTag = parsedLine[6];

                        if (!string.IsNullOrEmpty(parsedLine[7])) data.SetFileExtensions(parsedLine[7]);

                        mainWindow.AppDatas.Add(data);
                    }
                }
            }
            catch (Exception ex)
            {
                //データが存在しない場合など
                ErrorLogger.Log(ex);
            }

            mainWindow.AppDatas.ToList().ForEach(a => a.LoadFileDatas());

        }
    }
}
