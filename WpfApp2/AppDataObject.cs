using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
//using System.Drawing;
using System.Text;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace WpfApp2
{
    public class AppDataObject
    {

        //private string currentDir;

        ////アイコン画像の保存先ディレクトリ
        //private readonly string iconFileDir = "/data/icons/";

        private MainWindow mainWindow;

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
        public string FileExtension { get; private set; } = "";
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

        //Toggｌ記録用の終了確認フラグ
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

        public string GetLastTime
        {
            get
            {
                return LastTime.ToString("HH:mm"); ;
            }
        }


        public void SetLastLaunchedTime(string data)
        {
            if (!string.IsNullOrEmpty(data) && data != "-")
            {
                LastTime = DateTime.Parse(data);
            }
        }

        public int GetIndexOfTag
        {
            get
            {
                int index = mainWindow.TogglManager.Tags.IndexOf(LinkedTag);
                return index;
            }
            set
            {
                LinkedTag = mainWindow.TogglManager.Tags[value];
            }
        }

        public string GetTotalTime
        {
            get
            {
                return GetFormattedStringFromMinutes(TotalMinutes);
            }
        }

        public string GetTodaysTime
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

        public void SetFileExtensions(string input)
        {
            string[] parsed = input.Split('/');
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
        /// 記録情報を終了させる
        /// </summary>
        public void Exit()
        {
            IsRecordStarted = false;
            IsRunning = false;
            mainWindow.TogglManager.SetTimeEntry(this);
        }

        /// <summary>
        /// ファイル別の作業時間を読み込み
        /// </summary>
        public void LoadFileData()
        {
            string path = Directory.GetCurrentDirectory() + "/data/fileData/" + ProcessName + "_files.csv";
            //string path = "data/fileData\\" + ProcessName + "_files.csv";
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

        public void SaveFileData()
        {
            //String path = "data/fileData/" + ProcessName + "_files.csv";
            string path = Directory.GetCurrentDirectory() + "/data/fileData/" + ProcessName + "_files.csv";
            //string path = "data/fileData\\" + ProcessName + "_files.csv";
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

                //SaveIconImage(ImageSource);
                IconImage = new Image();
                using (MemoryStream s = new MemoryStream())
                {
                    icon.Save(s);
                    ImageSource = BitmapFrame.Create(s);
                }
                SaveIconImage(ImageSource);

            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }


        }

        public void SaveIconImage(ImageSource source)
        {
            //string uriPath = iconFileDir + $"{ProcessName}.png";
            string uriPath = Directory.GetCurrentDirectory() + "/data/icons/" + $"{ProcessName}.png";

            using (var fileStream = new FileStream(@uriPath, FileMode.Create))
            {
                BitmapEncoder encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create((BitmapSource)source));
                encoder.Save(fileStream);
            }
            Console.WriteLine();
        }

        public void LoadIconImage()
        {
            var bmpImage = new BitmapImage();
            string uriPath = Directory.GetCurrentDirectory() + "/data/icons/" + $"{ProcessName}.png";

            try
            {
                bmpImage.BeginInit();
                bmpImage.UriSource = new Uri(@uriPath, UriKind.Absolute);
                bmpImage.EndInit();
                ImageSource = bmpImage;
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
            
            MinutesFromLaunched += Properties.Settings.Default.CountInterval;

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

            //拡張機能
            if (string.IsNullOrEmpty(fileName) && Properties.Settings.Default.isAdditionalFileName)
            {
                string[] parsed0 = title.Split('-');
                if (parsed0.Length > 2)
                {
                    fileName = parsed0[0];
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
        private void AddFileData(string fileName)
        {
            //最大件数をオーバーしている場合、先頭の要素を削除
            if (Files.Count > Properties.Settings.Default.MaxFileNum)
            {
                Files.RemoveAt(0);
            }
            Files.Add(new FileData { Name = fileName });
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
