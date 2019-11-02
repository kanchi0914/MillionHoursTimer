using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace MHTimer
{
    public static class IconGetter
    {
        static readonly string iconFileDir = Settings.IconFileDir;
        static readonly string fileDataDir = Settings.FileDataDir;
        static readonly string currentDir = Settings.CurrentDir;

        /// <summary>
        /// ファイルパスからアイコンを読み込み、保存
        /// </summary>
        /// <param name="path"></param>
        public static void SetIconToNewAppData(string path, AppDataObject appData)
        {
            try
            {
                var img = Icon.ExtractAssociatedIcon(@path).ToBitmap();
                var imageSource = img.ToImageSource();
                appData.IconImageSource = imageSource;

                var savePath = currentDir + iconFileDir + $"{appData.ProcessName}.png";
                SaveIconImage(imageSource, savePath);
            }
            catch (FileNotFoundException e)
            {
                var defaultIconImagePath = currentDir + iconFileDir + $"defaultIcon.png";
                LoadIconImage(defaultIconImagePath);
            }
            catch (Exception ex)
            {
                ErrorLogger.ShowErrorMessage(ex);
            }
        }

        /// <summary>
        /// アイコンイメージの保存
        /// </summary>
        /// <param name="source">Imagesourceオブジェクト</param>
        /// <param name="path">保存先パス</param>
        public static void SaveIconImage(ImageSource source, string path)
        {
            using (var fileStream = new FileStream(@path, FileMode.Create))
            {
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create((BitmapSource)source));
                encoder.Save(fileStream);
            }
        }

        /// <summary>
        /// アイコン画像の読み込み
        /// </summary>
        /// <param name="path">読み込み先パス</param>
        public static ImageSource LoadIconImage(string path)
        {
            var bmpImage = new BitmapImage();

            //アクセス権握ったままだと削除できないので注意
            //ref:http://neareal.net/index.php?Programming%2F.NetFramework%2FWPF%2FWriteableBitmap%2FLoadReleaseableBitmapImage
            try
            {
                MemoryStream data = new MemoryStream(File.ReadAllBytes(@path));
                WriteableBitmap wbmp = new WriteableBitmap(BitmapFrame.Create(data));
                data.Close();
                return wbmp;
            }
            //アイコン画像が存在しない場合、デフォルトのアイコン画像を使用
            catch (FileNotFoundException e)
            {
                var defaultIconImagePath = currentDir + iconFileDir + $"defaultIcon.png";
                return LoadIconImage(defaultIconImagePath);
            }
            catch (Exception ex)
            {
                ErrorLogger.ShowErrorMessage(ex);
            }
            return null;
        }
        
        public static void RemoveIconImage(string processName)
        {
            var path = currentDir + iconFileDir + $"{processName}.png";
            File.Delete(@path);
        }
    }
}
