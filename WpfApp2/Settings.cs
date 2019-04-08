using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WpfApp2
{
    public static class Settings
    {

        //何秒ごとにカウントするか
        //デバッグ用
        public static int CountSeconds = 1;

        public static string DefaultAPIKey = "1610b6739c0904ad6774df3ddcf460ea";

        public static string IconFileDir = "data/icons/";

        public static string DataDir = "data";

        public static string AppDataFile = "data/appData.csv";



        /// <summary>
        /// 『〇〇時間○○分』の形にして返す
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
