using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WpfApp2
{
    public static class Settings
    {

        //============================================================
        //読み取り専用の設定
        //============================================================


        //何秒ごとにカウントするか
        //デバッグ用
        public static readonly int CountSeconds = 60;

        //public static readonly string DefaultAPIKey = "1610b6739c0904ad6774df3ddcf460ea";

        public static readonly string IconFileDir = "/data/icons/";

        //public static readonly string DataDir = "data";

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
        public static bool IsNotCountingOnSleep = true;
        //最小化しているアプリを計測対象に含めないかどうか
        public static bool IsCountingNotMinimized = false;
        //アクティブなアプリのみを計測対象にするか
        public static bool IsCountingOnlyActive = false;
        //ファイル名の拡張設定を有効にするか
        public static bool IsEnabledAdditionalFileNameSetting = false;
        //ファイル名の最大保存件数
        public static int MaxFileNum = 100;

        //計測間隔
        public static int CountInterval = 1;
        //何分立ってから記録を開始するか
        public static int MinCountStartTime = 1;

        //============================================================
        //その他保存用データ
        //============================================================
        public static string Date = "";

        public static string CurrentDir = "";

        public static bool IsLaunchedFromConsole = false;

        static Settings()
        {
            //CurrentDir = Directory.GetCurrentDirectory();
            CurrentDir = AppDomain.CurrentDomain.SetupInformation.ApplicationBase;
            Load();
        }

        /// <summary>
        /// アプリ起動時に呼ばれ、ファイルから設定を読み込む
        /// </summary>
        public static void Load()
        {
            APIKey = Properties.Settings.Default.APIKey;

            IsAutoLauch = Properties.Settings.Default.IsAutoLaunch;
            IsCountingNotMinimized = Properties.Settings.Default.isCountingNotMinimized;
            IsCountingOnlyActive = Properties.Settings.Default.isCountingOnlyActive;
            IsNotCountingOnSleep = Properties.Settings.Default.IsNotCountingOnSleep;
            IsEnabledAdditionalFileNameSetting = Properties.Settings.Default.isAdditionalFileName;
            MaxFileNum = Properties.Settings.Default.MaxFileNum;
            CountInterval = Properties.Settings.Default.CountInterval;
            MinCountStartTime = Properties.Settings.Default.MinCountStartTime;
            Date = Properties.Settings.Default.date;
        }

        /// <summary>
        /// 設定をファイルに保存する
        /// </summary>
        public static void Save()
        {
            Properties.Settings.Default.APIKey = APIKey;

            Properties.Settings.Default.IsAutoLaunch = IsAutoLauch;

            Properties.Settings.Default.isCountingNotMinimized = IsCountingNotMinimized;
            Properties.Settings.Default.isCountingOnlyActive = IsCountingOnlyActive;
            Properties.Settings.Default.IsNotCountingOnSleep = IsNotCountingOnSleep;
            Properties.Settings.Default.isAdditionalFileName = IsEnabledAdditionalFileNameSetting;
            Properties.Settings.Default.MaxFileNum = MaxFileNum;
            Properties.Settings.Default.CountInterval = CountInterval;
            Properties.Settings.Default.MinCountStartTime = MinCountStartTime;
            Properties.Settings.Default.date = Date;

            Properties.Settings.Default.Save();
        }

    }
}
