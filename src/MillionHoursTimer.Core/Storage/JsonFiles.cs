using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using MillionHoursTimer.Core.Csv;
using MillionHoursTimer.Core.Model;

namespace MillionHoursTimer.Core.Storage;

public static class JsonFiles
{
    public static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        // 日本語やパスをエスケープせずに書き、エディタで読めるようにする
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new JsonStringEnumConverter() },
    };

    public static T? Read<T>(string path) where T : class
    {
        if (!File.Exists(path)) return null;
        var (text, _) = TextFile.Read(path);
        return JsonSerializer.Deserialize<T>(text, Options);
    }

    public static void Write<T>(string path, T value) =>
        TextFile.WriteAtomic(path, JsonSerializer.Serialize(value, Options), TextFile.Utf8NoBom);
}

public static class SettingsStore
{
    /// <summary>設定を読む。壊れていたら退避してから既定値で始める。</summary>
    public static AppSettings Load(string path, List<string>? warnings = null)
    {
        AppSettings? settings = null;
        try
        {
            settings = JsonFiles.Read<AppSettings>(path);
        }
        catch (JsonException ex)
        {
            var backup = path + $".broken-{DateTime.Now:yyyyMMddHHmmss}";
            File.Copy(path, backup, overwrite: true);
            warnings?.Add($"settings.json を読み込めなかったため既定値で起動しました。元のファイルは {Path.GetFileName(backup)} に残しています ({ex.Message})");
        }
        settings ??= new AppSettings();
        settings.Normalize();
        return settings;
    }

    public static void Save(string path, AppSettings settings) => JsonFiles.Write(path, settings);
}
