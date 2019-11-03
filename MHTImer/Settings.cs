using System;
using System.Configuration;

namespace MHTimer
{
    public static class Settings
    {
        //設定保存先
        //C:\Users\アカウント名\AppData\Local\アプリ名\中略\user.config

        //============================================================
        //読み取り専用の設定
        //============================================================

        //何秒ごとにカウントするか
        //デバッグ用
        public static readonly int CountSeconds = 1;

        //public static readonly string DefaultAPIKey = "1610b6739c0904ad6774df3ddcf460ea";

        public static readonly string IconFileDir = "/data/icons/";

        public static readonly string FileDataDir = "/data/fileData/";

        public static readonly string LogDir = "/logs";


        //public static readonly string AppDataFile = "data/appData.csv";

        public static readonly string extentionSample =
            ".gif/.jpg/.jpeg/.png/.bmp/.ico/.tif/.tiff/.tga/.psd/.psb/.sai";

        //============================================================
        //ユーザー設定
        //============================================================

        public static string APIKey = "";

        //自動で起動するか
        public static bool IsAutoLauch = false;
        //スリープ時にカウントしないか
        public static bool StopsOnSleep = true;
        //最小化しているアプリを計測対象に含めないかどうか
        public static bool IsCountingNotMinimized = false;
        //アクティブなアプリのみを計測対象にするか
        public static bool IsCountingOnlyActive = false;
        //ファイル名の拡張設定を有効にするか
        public static bool IsEnabledAdditionalFileNameSetting = false;
        //ファイル名をスペースで区切るか
        public static bool IsDividingBySpace = false;
        //別ウィンドウ表示時、計測設定を無視するか
        public static bool IsIgnoringChindWindowSettings = false;
        //ファイル名の最大保存件数
        public static int MaxFileNum = 100;

        //計測間隔(秒)
        public static int CountingSecondsInterval = 1;
        //無操作判定時間(秒)
        public static int NoInputTime = 900;
        //何秒経ってから記録を開始するか
        public static int MinCountStartTime = 0;
        //何秒経過してからTogglに送信するか
        public static int MinSendTime = 60;

        //============================================================
        //その他保存用データ
        //============================================================
        public static string Date = "";

        public static string CurrentDir = "";

        public static bool IsLaunchedFromConsole = false;

        static Settings()
        {
            CurrentDir = AppDomain.CurrentDomain.SetupInformation.ApplicationBase;
            Load();

            //初めて起動された
            if (Date == "0001/01/01")
            {
                LoadDefaultSettings();
                Date = DateTime.Now.ToString("yyyy/MM/dd");
            }

        }

        /// <summary>
        /// アプリ起動時に呼ばれ、ファイルから設定を読み込む
        /// </summary>
        public static void Load()
        {
            APIKey = Properties.Settings.Default.APIKey;

            IsAutoLauch = Properties.Settings.Default.IsAutoLaunch;
            IsCountingNotMinimized = Properties.Settings.Default.IsCountingNotMinimized;
            IsCountingOnlyActive = Properties.Settings.Default.IsCountingOnlyActive;
            StopsOnSleep = Properties.Settings.Default.StopsOnSleep;
            IsEnabledAdditionalFileNameSetting = Properties.Settings.Default.IsEnableAdditionalFileName;
            IsDividingBySpace = Properties.Settings.Default.IsDividingBySpace;
            MaxFileNum = Properties.Settings.Default.MaxFileNum;
            CountingSecondsInterval = Properties.Settings.Default.CountingSecondsInterval;
            NoInputTime = Properties.Settings.Default.NoInputTime;
            MinCountStartTime = Properties.Settings.Default.MinCountStartTime;
            MinSendTime = Properties.Settings.Default.MinSendTime;
            Date = Properties.Settings.Default.Date;
            IsIgnoringChindWindowSettings = Properties.Settings.Default.isIgnoringChindsWindows;
        }

        public static void LoadDefaultSettings()
        {
            try
            {
                APIKey = Properties.Settings.Default.Properties["APIKey"].DefaultValue.ToString();
                Date = Properties.Settings.Default.Properties["Date"].DefaultValue.ToString();

                IsAutoLauch = bool.Parse(Properties.Settings.Default.Properties["IsAutoLaunch"].DefaultValue.ToString());
                IsCountingNotMinimized = bool.Parse(Properties.Settings.Default.Properties["IsCountingNotMinimized"].DefaultValue.ToString());
                IsCountingOnlyActive = bool.Parse(Properties.Settings.Default.Properties["IsCountingOnlyActive"].DefaultValue.ToString());
                StopsOnSleep = bool.Parse(Properties.Settings.Default.Properties["StopsOnSleep"].DefaultValue.ToString());
                IsEnabledAdditionalFileNameSetting = bool.Parse(Properties.Settings.Default.Properties["IsEnableAdditionalFileName"].DefaultValue.ToString());
                IsDividingBySpace = bool.Parse(Properties.Settings.Default.Properties["IsDividingBySpace"].DefaultValue.ToString());
                IsIgnoringChindWindowSettings = bool.Parse(Properties.Settings.Default.Properties["isIgnoringChindsWindows"].DefaultValue.ToString());

                MaxFileNum = int.Parse(Properties.Settings.Default.Properties["MaxFileNum"].DefaultValue.ToString());
                CountingSecondsInterval = int.Parse(Properties.Settings.Default.Properties["CountingSecondsInterval"].DefaultValue.ToString());
                NoInputTime = int.Parse(Properties.Settings.Default.Properties["NoInputTime"].DefaultValue.ToString());
                MinCountStartTime = int.Parse(Properties.Settings.Default.Properties["MinCountStartTime"].DefaultValue.ToString());
                MinSendTime = int.Parse(Properties.Settings.Default.Properties["MinSendTime"].DefaultValue.ToString());

                Save();
            }
            catch (Exception ex)
            {
                ErrorLogger.Log(ex);
            }
            
        }

        /// <summary>
        /// 設定をファイルに保存する
        /// </summary>
        public static void Save()
        {
            Properties.Settings.Default.APIKey = APIKey;

            Properties.Settings.Default.IsAutoLaunch = IsAutoLauch;

            Properties.Settings.Default.IsCountingNotMinimized = IsCountingNotMinimized;
            Properties.Settings.Default.IsCountingOnlyActive = IsCountingOnlyActive;
            Properties.Settings.Default.StopsOnSleep = StopsOnSleep;
            Properties.Settings.Default.IsEnableAdditionalFileName = IsEnabledAdditionalFileNameSetting;
            Properties.Settings.Default.IsDividingBySpace = IsDividingBySpace;
            Properties.Settings.Default.MaxFileNum = MaxFileNum;
            Properties.Settings.Default.CountingSecondsInterval = CountingSecondsInterval;
            Properties.Settings.Default.MinCountStartTime = MinCountStartTime;
            Properties.Settings.Default.NoInputTime = NoInputTime;
            Properties.Settings.Default.Date = Date;
            Properties.Settings.Default.MinSendTime = MinSendTime;
            Properties.Settings.Default.isIgnoringChindsWindows = IsIgnoringChindWindowSettings;

            Properties.Settings.Default.Save();
        }

    }
}
