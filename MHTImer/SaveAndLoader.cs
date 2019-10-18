using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
                    sw.WriteLine($"アプリケーション名,今日の起動時間(分),累積起動時間(分),最終起動日時," +
                        $"toggle連携フラグ,連携プロジェクト名,連携タグ名, ファイル拡張子");
                    foreach (AppDataObject appData in mainWindow.AppDatas)
                    {
                        sw.WriteLine($"{appData.ProcessName}," +
                            $"{appData.TodaysTime}," +
                            $"{appData.TotalTime}," +
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
                Console.WriteLine(ex.Message);
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
                        AppDataObject data = new AppDataObject(mainWindow, parsedLine[0])
                        {
                            DisplayedName = parsedLine[0],
                            TodaysTime = TimeSpan.Parse(parsedLine[1]),
                            TotalTime = TimeSpan.Parse(parsedLine[2]),
                            //[3]
                            IsLinkedToToggl = bool.Parse(parsedLine[4]),
                            LinkedProjectName = parsedLine[5],
                            LinkedTag = parsedLine[6]
                        };
                        data.SetLastRunningTIme(parsedLine[3]);
                        data.SetFileExtensions(parsedLine[7]);
                        mainWindow.AppDatas.Add(data);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }

            mainWindow.AppDatas.ToList().ForEach(a => a.LoadFileDatas());

        }
    }
}
