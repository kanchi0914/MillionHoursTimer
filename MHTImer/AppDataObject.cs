using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Text.RegularExpressions;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Interop;
using System.Windows;
using System.Runtime.InteropServices;

namespace MHTimer
{
    public class AppDataObject
    {

        private MainWindow mainWindow;

        readonly string iconFileDir = Settings.IconFileDir;
        readonly string fileDataDir = Settings.FileDataDir;

        string currentDir = Settings.CurrentDir;
        
        //プロセスの名前（不変）
        public string ProcessName { get; } = "";

        public List<string> Titles { get; set; } = new List<string>();

        //ウィンドウに表示される名前
        public string DisplayedName { get; set; } = "";

        //起動した時間
        public DateTime LaunchedTime { get; set; }

        //最後に起動を確認した時刻
        public DateTime LastRunningTime { get; private set; }

        public void SetLastRunningTime(string data)
        {
            if (!string.IsNullOrEmpty(data) && data != "-")
            {
                LastRunningTime = DateTime.Parse(data);
            }
        }

        //ファイル設定
        public List<string> FileExtensions { get; private set; } = new List<string>();
        public ObservableCollection<FileDataObject> Files { get; set; } = new ObservableCollection<FileDataObject>();

        //アイコン画像
        public ImageSource IconImageSource { get; set; }

        //時間データ
        public TimeSpan TodaysTime { get; set; }
        public TimeSpan TotalTime { get; set; }
        public TimeSpan TimeFromLastLaunched
        {
            get => LastRunningTime - LaunchedTime;
        }

        public bool IsRecoding { get { return isRecoding; } set
            {
                var a = 0;
                isRecoding = value;
            } }
        private bool isRecoding = false;
        public bool IsRunning { get; set; } = false;

        //Toggl設定
        public bool IsLinkedToToggl { get; set; } = false;
        public string LinkedProjectName { get; set; } = "";
        public string LinkedTag { get; set; } = "";
        public List<string> ProjectNames { get; set; } = new List<string>() { "" };
        public List<string> TagNames { get; set; } = new List<string>() { "" };

        public string Color
        {
            get
            {
                if (IsRecoding) return "#ffc0cb";
                return "White";
            }
        }

        public string NameOfProject
        {
            get => LinkedProjectName;
            set => LinkedProjectName = value;
        }

        public string NameOfTag
        {
            get => LinkedTag;
            set => LinkedTag = value;
        }

        public string GetTotalTimeText
        {
            get => GetFormattedStringFromTimeSpan(TotalTime);
        }

        public string GetTodaysTimeText
        {
            get => GetFormattedStringFromTimeSpan(TodaysTime);
        }


        public string GetLastLaunchedTimeText
        {
            get
            {
                string lastDate = LastRunningTime.ToString("yyyy");
                if (lastDate != "0001")
                {
                    return LastRunningTime.ToString("yyyy/MM/dd hh:mm");
                }
                else
                {
                    return "-";
                }
            }
        }

        /// <summary>
        /// ファイル拡張子の設定をstring型で返す(保存用)
        /// </summary>
        /// <returns></returns>
        public string GetFileExtensionText()
        {
            var extentionText = string.Join("/", FileExtensions.ToArray());
            return extentionText;
        }

        /// <summary>
        /// ファイル拡張子を設定
        /// </summary>
        /// <param name="extensionTexts">半角スラッシュ区切りの拡張子文字列</param>
        public void SetFileExtensions(string extensionTexts)
        {
            string[] parsed = extensionTexts.Split('/');
            if (parsed.Length > 0)
            {
                FileExtensions = new List<string>();
                foreach (string s in parsed)
                {
                    if (!s.Contains('.')) continue;
                    FileExtensions.Add(s);
                }
            }
        }


        public AppDataObject(MainWindow mainWindow, string processName)
        {
            this.mainWindow = mainWindow;
            this.ProcessName = processName;
            Init();
        }

        public void Init()
        {
            var iconImagePath = currentDir + iconFileDir + $"{ProcessName}.png";
            IconImageSource = IconGetter.LoadIconImage(iconImagePath);
        }

        /// <summary>
        /// 設定した分毎に呼ばれ、時間を追加
        /// 実際に記録されるのは一定時間が経ってから
        /// </summary>
        /// <param name="filename"></param>
        public void AccumulateTimes()
        {
            if (!IsRunning)
            {
                LaunchedTime = DateTime.Now;
                IsRunning = true;
            }

            LastRunningTime = DateTime.Now;

            if (TimeFromLastLaunched.TotalSeconds >= Settings.MinCountStartTime)
            {
                //一度だけ、時間の差分を足す
                if (!IsRecoding)
                {
                    AccumulateTime(Settings.MinCountStartTime);
                    IsRecoding = true;
                }
                else
                {
                    AccumulateTime(Settings.CountingSecondsInterval);
                }
            }

            AccumulateTimeToFileDatas();

        }

        /// <summary>
        /// データの時間を更新
        /// </summary>
        /// <param name="seconds"></param>
        public void AccumulateTime(int seconds)
        {
            if (HasDayChanged()) TodaysTime = new TimeSpan(0, 0, 0);
            TotalTime = TotalTime.Add(TimeSpan.FromSeconds(seconds));
            TodaysTime = TodaysTime.Add(TimeSpan.FromSeconds(seconds));
        }

        /// <summary>
        /// 全てのファイルデータに対し、時間を累積
        /// </summary>
        public void AccumulateTimeToFileDatas()
        {
            var titles = mainWindow.WindowTitleHolder.GetWindowTitlesByProcessName(ProcessName);

            var countedFileNames = new HashSet<string>();
            foreach (string title in titles)
            {
                var fileName = GetFileNameByTitle(title);
                if (string.IsNullOrEmpty(fileName) || countedFileNames.Contains(fileName)) continue;
                countedFileNames.Add(fileName);
                AccumulateTimeToFileData(fileName);
            }
        }

        /// <summary>
        /// 計測を終了し、TimeEntryを送信
        /// </summary>
        public void Exit()
        {
            if (IsRecoding)
            {
                if (IsLinkedToToggl && TimeFromLastLaunched.TotalSeconds > Settings.MinSendTime)
                {
                    mainWindow.TogglManager.SetTimeEntry(this);
                }
            }
            LastRunningTime = DateTime.Now;
            IsRunning = false;
            IsRecoding = false;
        }

        /// <summary>
        /// ファイル別の作業時間を読み込み
        /// </summary>
        public void LoadFileDatas()
        {
            string path = currentDir + fileDataDir + ProcessName + "_files.csv";
            try
            {
                using (StreamReader reader = new StreamReader(@path, Encoding.UTF8))
                {
                    reader.ReadLine();
                    while (!reader.EndOfStream)
                    {
                        string line = reader.ReadLine();
                        string[] parsedLine = line.Split(',');
                        FileDataObject file = new FileDataObject()
                        {
                            Name = parsedLine[0],
                            TotalTime = TimeSpan.Parse(parsedLine[1])
                        };
                        Files.Add(file);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }


        public string GetFileNameByTitle(string title)
        {
            foreach (var extentionName in FileExtensions)
            {
                if (!extentionName.Contains('.')) continue;
                var text = $@"\s*(.+\{extentionName})";
                var match = Regex.Match(title, text);
                if (match.Success)
                {
                    if (Settings.IsDividingBySpace)
                    {
                        foreach (string s in match.Groups[1].Value.Split(' '))
                        {
                            var match2 = Regex.Match(s, text);
                            if (match2.Success) return match2.Value;
                        }
                    }
                    return match.Value;
                }
            }

            if (Properties.Settings.Default.IsEnableAdditionalFileName)
            {
                return GetAdditionalFileNameByTitle(title);
            }

            return "";
        }

        public string GetAdditionalFileNameByTitle(string title)
        {
            Match match = Regex.Match(title, @"(.+?)\s+-");
            if (match.Groups[1].Success)
            {
                if (Settings.IsDividingBySpace)
                {
                    return match.Groups[1].Value.Split(' ')[0];
                }
                return match.Groups[1].Value;
            }

            return "";
        }

        /// <summary>
        /// ファイルデータに対し、時間を累積
        /// </summary>
        /// <param name="windowTitle"></param>
        public void AccumulateTimeToFileData(string fileName)
        {
            if (!string.IsNullOrEmpty(fileName))
            {
                FileDataObject file = Files.ToList().Find(f => f.Name == fileName);
                if (file == null)
                {
                    file = CreateFileDate(fileName, new TimeSpan(0, 0, 0));
                    AddFileDataToList(file);
                }
                file.AccumulateTime();
            }
        }


        public void AddFileDataToList(FileDataObject fileData)
        {
            //最大件数をオーバーしている場合、先頭の要素を削除
            if (Files.Count >= Properties.Settings.Default.MaxFileNum)
            {
                Files.RemoveAt(0);
            }
            lock (fileData)
            {
                mainWindow.Dispatcher.BeginInvoke(new Action(() => Files.Add(fileData)));
            }
        }

        /// <summary>
        /// ファイルデータを削除
        /// </summary>
        /// <param name="fileData"></param>
        public void RemoveFileDataFromList(FileDataObject fileData)
        {
            lock (fileData)
            {
                Files.Remove(fileData);
            }
            SaveFileDatas();
        }

        public void RemoveAllFileData()
        {
            lock (Files)
            {
                Files.Clear();
            }
            string path = currentDir + fileDataDir + ProcessName + "_files.csv";
            File.Delete(@path);
        }

        /// <summary>
        /// ファイル別の作業時間データを保存
        /// </summary>
        public void SaveFileDatas()
        {
            string path = currentDir + fileDataDir + ProcessName + "_files.csv";
            if (Files.Count > 0)
            {
                try
                {
                    using (var sw = new System.IO.StreamWriter(@path, false, Encoding.UTF8))
                    {
                        sw.WriteLine($"ファイル名,累積作業時間");
                        foreach (FileDataObject file in Files)
                        {
                            sw.WriteLine($"{file.Name},{file.TotalTime}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
            }
        }

        private bool HasDayChanged()
        {
            var currentDate = DateTime.Now.ToString("yyyy/MM/dd");
            return currentDate != LastRunningTime.ToString("yyyy/MM/dd");
        }

        public static FileDataObject CreateFileDate(string fileName, TimeSpan totalTime)
        {
            return new FileDataObject { Name = fileName, TotalTime = totalTime };
        }

        public static string GetFormattedStringFromTimeSpan(TimeSpan timeSpan)
        {
            if (timeSpan < TimeSpan.FromSeconds(1)) return "-";
            var hours = string.Format("{0, 4}", timeSpan.Hours);
            var minutes = string.Format("{0, 2}", timeSpan.Minutes);
            var seconds = string.Format("{0, 2}", timeSpan.Seconds);
            return $"{hours}時間{minutes}分{seconds}秒";
        }

        public override bool Equals(object obj)
        {
            if (obj == null || this.GetType() != obj.GetType())
            {
                return false;
            }
            AppDataObject data = (AppDataObject)obj;
            return (data.ProcessName == this.ProcessName);
        }

        public override int GetHashCode()
        {
            return ProcessName.GetHashCode();
        }

    }
}
