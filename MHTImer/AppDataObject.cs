using Microsoft.WindowsAPICodePack.Shell;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
//using System.Drawing;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

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
        public DateTime LastTime { get; private set; }

        //ファイル設定
        public List<string> FileExtensions { get; private set; } = new List<string>();
        public ObservableCollection<FileDataObject> Files { get; private set; } = new ObservableCollection<FileDataObject>();

        //アイコン画像
        public ImageSource IconImageSource { get; private set; }
        //public Image IconImage { get; private set; }

        //時間データ
        public TimeSpan TodaysTime { get; set; }
        public TimeSpan TotalTime { get; set; }
        public TimeSpan TimeFromLastLaunched { get; set; }
        public bool IsRecordStarted { get; set; } = false;

        //Toggl記録用の終了確認フラグ
        public bool IsRunning { get; set; } = false;

        //Toggl設定
        public bool IsLinkedToToggle { get; set; } = false;
        public string LinkedProjectName { get; set; } = "";
        public string LinkedTag { get; set; } = "";

        //名前
        public List<string> ProjectNames { get; set; } = new List<string>() { "" };
        public List<string> TagNames { get; set; } = new List<string>() { "" };

        public string Color
        {
            get
            {
                if (IsRecordStarted) return "#ffc0cb";
                return "White";
            }
        }

        public string NameOfProject
        {
            get
            {
                return LinkedProjectName;
            }
            set
            {
                LinkedProjectName = value;
            }
        }

        public string NameOfTag
        {
            get
            {
                return LinkedTag;
            }
            set
            {
                LinkedTag = value;
            }
        }

        public string GetTotalTime
        {
            get
            {
                return GetFormattedStringFromTimeSpan(TotalTime);
            }
        }

        public string GetTodaysTime
        {
            get
            {
                return GetFormattedStringFromTimeSpan(TodaysTime);
            }
        }


        public string GetLastLaunchedTime
        {
            get
            {
                string lastDate = LastTime.ToString("yyyy");
                if (lastDate != "0001")
                {
                    return LastTime.ToString("yyyy/MM/dd hh:mm");
                }
                else
                {
                    return "-";
                }
            }
        }

        public void SetLastLaunchedTime(string data)
        {
            if (!string.IsNullOrEmpty(data) && data != "-")
            {
                LastTime = DateTime.Parse(data);
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
            LoadIconImage(iconImagePath);
        }

        /// <summary>
        /// 計測を終了し、TimeEntryを送信
        /// </summary>
        public void Exit()
        {
            if (IsRecordStarted)
            {
                IsRunning = false;
                if (IsLinkedToToggle)
                {
                    mainWindow.TogglManager.SetTimeEntry(this);
                }
            }
            IsRecordStarted = false;
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
                    FileExtensions.Add(s);
                }
            }
        }

        /// <summary>
        /// ファイル別の作業時間を読み込み
        /// </summary>
        public void LoadFileDatas()
        {
            string path = currentDir + fileDataDir + ProcessName + "_files.csv";
            Console.WriteLine(path);

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

        /// <summary>
        /// ファイルパスからアイコンを読み込み、保存
        /// </summary>
        /// <param name="path"></param>
        public void SetIcon(string path)
        {
            System.Drawing.Icon icon;
            try
            {
                icon = System.Drawing.Icon.ExtractAssociatedIcon(@path);

                using (MemoryStream s = new MemoryStream())
                {
                    icon.Save(s);
                    IconImageSource = BitmapFrame.Create(s);
                }
                var savePath = currentDir + iconFileDir + $"{ProcessName}.png";
                SaveIconImage(IconImageSource, savePath);
            }
            catch (FileNotFoundException e)
            {
                var defaultIconImagePath = currentDir + iconFileDir + $"defaultIcon.png";
                LoadIconImage(defaultIconImagePath);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        /// <summary>
        /// アイコンイメージの保存
        /// </summary>
        /// <param name="source">Imagesourceオブジェクト</param>
        /// <param name="path">保存先パス</param>
        public void SaveIconImage(ImageSource source, string path)
        {
            //string uriPath = iconFileDir + $"{ProcessName}.png";
            //string uriPath = currentDir + iconFileDir + $"{ProcessName}.png";
            using (var fileStream = new FileStream(@path, FileMode.Create))
            {
                BitmapEncoder encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create((BitmapSource)source));
                encoder.Save(fileStream);
            }
        }

        /// <summary>
        /// アイコン画像の読み込み
        /// </summary>
        /// <param name="path">読み込み先パス</param>
        public void LoadIconImage(string path)
        {
            var bmpImage = new BitmapImage();

            try
            {
                bmpImage.BeginInit();
                bmpImage.UriSource = new Uri(@path, UriKind.Absolute);
                //bmpImage.UriSource = new Uri(@path);
                bmpImage.EndInit();
                IconImageSource = bmpImage;
            }
            //アイコン画像が存在しない場合、デフォルトのアイコン画像を使用
            catch (FileNotFoundException e)
            {
                var defaultIconImage = currentDir + iconFileDir + $"defaultIcon.png";
                LoadIconImage(defaultIconImage);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
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
            else
            {
                var pre = TimeFromLastLaunched;
                var second = pre.Seconds + Settings.CountingSecondsInterval;
                TimeFromLastLaunched = new TimeSpan(pre.Hours, pre.Minutes, second);
            }
 
            if (TimeFromLastLaunched.Minutes >= Settings.MinCountStartTime)
            {
                //一度だけ、時間の差分を足す
                if (!IsRecordStarted)
                {
                    AccumulateTime(Settings.MinCountStartTime);
                    IsRecordStarted = true;
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
            TotalTime = TotalTime.Add(TimeSpan.FromSeconds(seconds));
            if (IsDayChanged()) TodaysTime = new TimeSpan(0, 0, 0);
            TodaysTime = TodaysTime.Add(TimeSpan.FromSeconds(seconds));
            LastTime = DateTime.Now;
        }

        /// <summary>
        /// 全てのファイルデータに対し、時間を累積
        /// </summary>
        public void AccumulateTimeToFileDatas()
        {
            //ウィンドウタイトルを取得
            WindowTitles2 windowTitles2 = new WindowTitles2();
            var titles = windowTitles2.Get(ProcessName);

            var isCounteds = new List<string>();
            foreach (string title in titles)
            {
                if (isCounteds.Contains(title)) continue;
                else isCounteds.Add(title);
                var fileName = GetFileNameByTitle(title);
                AccumulateTimeToFileData(fileName);
            }
        }

        public string GetFileNameByTitle(string title)
        {
            var fileName = "";
            var parsed = title.Split(' ');

            foreach (string s in parsed)
            {
                if (FileExtensions.Count > 0 && FileExtensions.Any(f => s.Contains(f)))
                    fileName = s;
            }

            if (Properties.Settings.Default.IsEnableAdditionalFileName &&
                string.IsNullOrEmpty(fileName))
            {
                fileName = GetAdditionalFileNameByTitle(title);
            }

            return fileName;
        }

        public string GetAdditionalFileNameByTitle(string title)
        {
            var fileName = "";
            var parsedByHyphen = title.Split('-');
            if (parsedByHyphen.Length > 1)
            {
                if (Settings.IsDividingBySpace)
                {
                    var parsedBySpace = title.Split(' ');
                    fileName = parsedBySpace[0];
                }
                else
                {
                    fileName = parsedByHyphen[0];
                }
            }
            return fileName;
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
            //Files.Add(fileData);
            mainWindow.Dispatcher.BeginInvoke(new Action(() => Files.Add(fileData)));
                //.Dispatcher.BeginInvoke(new Action(() => mainWindow.listView.Items.Add(appData)));
        }

        /// <summary>
        /// ファイルデータを削除
        /// </summary>
        /// <param name="fileData"></param>
        public void RemoveFileDataFromList(FileDataObject fileData)
        {
            Files.Remove(fileData);
            SaveFileDatas();
        }

        private bool IsDayChanged()
        {
            var currentDate = DateTime.Now.ToString("yyyy/MM/dd");
            return currentDate != LastTime.ToString("yyyy/MM/dd");
        }

        public static FileDataObject CreateFileDate(string fileName, TimeSpan totalTime)
        {
            return new FileDataObject { Name = fileName, TotalTime = totalTime };
        }

        public static string GetFormattedStringFromTimeSpan(TimeSpan timeSpan)
        {
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
