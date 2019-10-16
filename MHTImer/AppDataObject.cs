using Microsoft.WindowsAPICodePack.Shell;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
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

        //最後に起動した日
        //public string LastDate { get; private set; } = "";

        //最後に起動を確認した時刻
        public DateTime LastTime { get; private set; }

        //ファイル設定
        public List<string> FileExtensions { get; private set; } = new List<string>();
        public List<FileData> Files { get; private set; } = new List<FileData>();

        //アイコン画像
        public ImageSource ImageSource { get; private set; }
        public Image IconImage { get; private set; }

        //時間データ
        public int TodaysMinutes { get; set; }
        public int TotalMinutes { get; set; }
        public int MinutesFromLaunched { get; set; }
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

        public AppDataObject(MainWindow mainWindow, string processName)
        {
            this.mainWindow = mainWindow;
            this.ProcessName = processName;
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

        public void Init()
        {
            var iconImagePath = currentDir + iconFileDir + $"{ProcessName}.png";
            LoadIconImage(iconImagePath);
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

        public string TotalTime
        {
            get
            {
                return GetFormattedStringFromMinutes(TotalMinutes);
            }
        }

        public string TodaysTime
        {
            get
            {
                return GetFormattedStringFromMinutes(TodaysMinutes);
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
        public void LoadFileData()
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
                        FileData file = new FileData()
                        {
                            Name = parsedLine[0],
                            TotalMinutes = int.Parse(parsedLine[1])
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
                        foreach (FileData file in Files)
                        {
                            sw.WriteLine($"{file.Name},{file.TotalMinutes}");
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
                    ImageSource = BitmapFrame.Create(s);
                }
                var savePath = currentDir + iconFileDir + $"{ProcessName}.png";
                SaveIconImage(ImageSource, savePath);
            }
            catch (FileNotFoundException e)
            {
                //MessageBox.Show(e.ToString());
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
                bmpImage.EndInit();
                ImageSource = bmpImage;
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
        public void AccumulateMinutes()
        {
            if (!IsRunning)
            {
                LaunchedTime = DateTime.Now;
                IsRunning = true;
            }
            else
            {
                MinutesFromLaunched += Properties.Settings.Default.CountInterval;
            }
 
            if (MinutesFromLaunched >= Properties.Settings.Default.MinCountStartTime)
            {
                //一度だけ、時間の差分を足す
                if (!IsRecordStarted)
                {
                    AddMinutes(Properties.Settings.Default.MinCountStartTime);
                    IsRecordStarted = true;
                }
                else
                {

                    AddMinutes(Properties.Settings.Default.CountInterval);
                }
            }

        }

        /// <summary>
        /// データの時間を更新
        /// </summary>
        /// <param name="minutes"></param>
        public void AddMinutes(int minutes)
        {
            TotalMinutes += minutes;
            string currentDate = DateTime.Now.ToString("yyyy/MM/dd");
            if (currentDate != LastTime.ToString("yyyy/MM/dd"))
            {
                //var minute = DateTime.Now.Minute;
                TodaysMinutes = 0;
                TodaysMinutes += minutes;
            }
            else
            {
                TodaysMinutes += minutes;
            }

            LastTime = DateTime.Now;
        }

        /// <summary>
        /// ファイルデータに対し、時間を累積
        /// </summary>
        /// <param name="windowTitle"></param>
        public void AccumulateMinuteToFileData(string windowTitle)
        {
            string fileName = "";
            string title = windowTitle;
            string[] parsed = title.Split(' ');

            foreach (string s in parsed)
            {
                foreach (string f in FileExtensions)
                {
                    //登録した拡張子に合致するものがあった
                    if (s.Contains(f))
                    {
                        fileName = s;
                        break;
                    }
                }
            }

            //ハイフン区切り
            if (string.IsNullOrEmpty(fileName) && Properties.Settings.Default.isAdditionalFileName)
            {
                string[] parsedByHyphen = title.Split('-');
                if (parsedByHyphen.Length > 1)
                {
                    if (Settings.IsDividingBySpace)
                    {
                        string[] parsedBySpace = title.Split(' ');
                        fileName = parsedBySpace[0];
                    }
                    else
                    {
                        fileName = parsedByHyphen[0];
                    }
                }
            }

            if (!string.IsNullOrEmpty(fileName))
            {
                FileData file = Files.Find(f => f.Name == fileName);
                //ファイルが既に存在すれば時間を追加し、なければ作成
                if (file != null)
                {
                    file.AccumulateMinute();
                }
                else
                {
                    AddFileData(fileName);
                }
            }
        }

        /// <summary>
        /// 全てのファイルデータに対し、時間を累積
        /// </summary>
        public void AccumulateMinuteToFileDatas()
        {
            //ウィンドウタイトルを取得
            WindowTitles2 windowTitles2 = new WindowTitles2();
            var titles = windowTitles2.Get(ProcessName);

            foreach (string t in titles)
            {
                AccumulateMinuteToFileData(t);
            }

        }

        /// <summary>
        /// ファイルデータを追加
        /// </summary>
        /// <param name="fileName"></param>
        public void AddFileData(string fileName, int minutes = 0)
        {
            //最大件数をオーバーしている場合、先頭の要素を削除
            if (Files.Count >= Properties.Settings.Default.MaxFileNum)
            {
                Files.RemoveAt(0);
            }
            Files.Add(new FileData { Name = fileName, TotalMinutes = minutes });
        }

        /// <summary>
        /// ファイルデータを削除
        /// </summary>
        /// <param name="fileData"></param>
        public void RemoveFileData(FileData fileData)
        {
            Files.Remove(fileData);
            SaveFileDatas();
        }


        public class FileData
        {
            public string Name { get; set; } = "";
            public int TotalMinutes { get; set; }
            public int MinutesFromLaunched { get; set; }
            public bool IsCountStarted { get; set; } = false;
            public bool IsCounted { get; set; } = false;

            public string GetTime
            {
                get
                {
                    return GetFormattedStringFromMinutes(TotalMinutes);
                }
            }

            public void AccumulateMinute()
            {
                //同名ファイルが複数回計測されるのをを防ぐ
                if (IsCounted) return;

                MinutesFromLaunched += Settings.CountInterval;

                //指定した時間が経過していたら、データの記録を開始
                if (MinutesFromLaunched >= Settings.MinCountStartTime)
                {
                    if (!IsCountStarted)
                    {
                        AddMinutes(Settings.MinCountStartTime);
                        IsCountStarted = true;
                    }
                    else
                    {
                        AddMinutes(Settings.CountInterval);
                    }
                }
            }

            public void AddMinutes(int minutes)
            {
                TotalMinutes += minutes;
            }

            public override bool Equals(object obj)
            {
                if (obj == null || this.GetType() != obj.GetType())
                {
                    return false;
                }
                FileData data = (FileData)obj;
                return (data.Name == this.Name);
            }

            public override int GetHashCode()
            {
                return Name.GetHashCode();
            }
        }

        /// <summary>
        /// 分のデータを、指定の形式に変換して返す
        /// </summary>
        /// <param name="minutes"></param>
        /// <returns></returns>
        public static string GetFormattedStringFromMinutes(int minutes)
        {
            TimeSpan ts = new TimeSpan(0, minutes, 0);
            if (ts.Hours == 0 && ts.Minutes == 0)
            {
                return ("-");
            }
            else
            {
                var hours = ts.Days * 24 + ts.Hours;
                return ($"{hours.ToString().PadLeft(2)}時間　{ts.Minutes.ToString().PadLeft(2)}分");
            }
        }
    }
}
