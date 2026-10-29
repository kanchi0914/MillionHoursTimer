using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Drawing = System.Drawing;

namespace MillionHoursTimer.Platform;

internal static class IconHelper
{
    private static readonly Uri DefaultIconUri = new("pack://application:,,,/Resources/defaultIcon.png");
    private static ImageSource? _default;

    public static ImageSource DefaultIcon => _default ??= Freeze(new BitmapImage(DefaultIconUri));

    /// <summary>実行ファイルのアイコンを PNG にして保存する</summary>
    public static bool TrySaveExeIcon(string exePath, string pngPath)
    {
        try
        {
            using var icon = Drawing.Icon.ExtractIcon(exePath, 0, 32) ?? Drawing.Icon.ExtractAssociatedIcon(exePath);
            if (icon == null) return false;
            using var bitmap = icon.ToBitmap();
            Directory.CreateDirectory(Path.GetDirectoryName(pngPath)!);
            bitmap.Save(pngPath, Drawing.Imaging.ImageFormat.Png);
            return true;
        }
        catch (Exception ex) when (ex is IOException or ArgumentException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>PNG を読む。ファイルを掴んだままにしないよう、メモリに読み込んでから使う。</summary>
    public static ImageSource Load(string pngPath)
    {
        try
        {
            if (!File.Exists(pngPath)) return DefaultIcon;
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = new MemoryStream(File.ReadAllBytes(pngPath));
            image.EndInit();
            return Freeze(image);
        }
        catch (Exception ex) when (ex is IOException or NotSupportedException or UnauthorizedAccessException)
        {
            return DefaultIcon;
        }
    }

    private static ImageSource Freeze(BitmapImage image)
    {
        image.Freeze();
        return image;
    }
}
