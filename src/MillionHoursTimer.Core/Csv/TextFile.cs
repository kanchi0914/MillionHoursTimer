using System.Text;

namespace MillionHoursTimer.Core.Csv;

public static class TextFile
{
    static TextFile()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    /// <summary>Excel で開いても文字化けしないよう、BOM 付き UTF-8 で書く</summary>
    public static readonly Encoding Utf8Bom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);

    public static readonly Encoding Utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    private static readonly Encoding StrictUtf8 = new UTF8Encoding(false, throwOnInvalidBytes: true);

    /// <summary>
    /// ファイルを読む。UTF-8 として読めなければ Shift-JIS とみなす
    /// (Excel で上書き保存すると Shift-JIS になることがあるため)。
    /// </summary>
    public static (string Text, bool IsUtf8) Read(string path)
    {
        byte[] bytes;
        using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
        using (var ms = new MemoryStream())
        {
            fs.CopyTo(ms);
            bytes = ms.ToArray();
        }

        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
        {
            return (Utf8NoBom.GetString(bytes, 3, bytes.Length - 3), true);
        }

        try
        {
            return (StrictUtf8.GetString(bytes), true);
        }
        catch (DecoderFallbackException)
        {
            return (Encoding.GetEncoding(932).GetString(bytes), false);
        }
    }

    /// <summary>一時ファイルに書いてから置き換える。書き込み途中で落ちても元のファイルは壊れない。</summary>
    public static void WriteAtomic(string path, string content, Encoding? encoding = null)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, content, encoding ?? Utf8Bom);
        File.Move(tmp, path, overwrite: true);
    }
}
