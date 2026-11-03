using System.Xml.Linq;
using MillionHoursTimer.Core.Csv;
using MillionHoursTimer.Core.Model;

namespace MillionHoursTimer.Core.Legacy;

public sealed record LegacyFile(string Name, long Seconds);

public sealed record LegacyApp(
    string Process,
    long TotalSeconds,
    bool TogglEnabled,
    string TogglProject,
    string TogglTag,
    List<string> Extensions,
    string? IconPath,
    List<LegacyFile> Files);

/// <summary>旧バージョンの user.config の内容</summary>
public sealed record LegacySettings(IReadOnlyDictionary<string, string> Values)
{
    public string? Get(string name) => Values.GetValueOrDefault(name);

    public string ApiKey => Get("APIKey") ?? "";

    /// <summary>新しい設定に反映する (値がないものは既定値のまま)</summary>
    public void ApplyTo(AppSettings s)
    {
        var onlyActive = GetBool("IsCountingOnlyActive");
        var notMinimized = GetBool("IsCountingNotMinimized");
        if (onlyActive is not null || notMinimized is not null)
        {
            s.CountMode = onlyActive == true ? CountMode.ActiveOnly
                : notMinimized == true ? CountMode.NotMinimized
                : CountMode.All;
        }
        if (GetInt("CountingSecondsInterval") is { } interval) s.CountIntervalSeconds = interval;
        if (GetInt("NoInputTime") is { } idle) s.IdleTimeoutSeconds = idle;
        if (GetInt("MinCountStartTime") is { } min) s.MinRecordSeconds = min;
        if (GetInt("MinSendTime") is { } send) s.TogglMinSeconds = send;
        if (GetBool("IsEnableAdditionalFileName") is { } hyphen) s.UseHyphenFileName = hyphen;
        if (GetBool("IsDividingBySpace") is { } space) s.SplitFileNameBySpace = space;
        if (GetBool("isIgnoringChindsWindows") is { } ignore) s.IgnoreWindowStateForFiles = ignore;
        if (GetBool("IsAutoLaunch") is { } auto) s.AutoStart = auto;
        s.Normalize();
    }

    private bool? GetBool(string name) => bool.TryParse(Get(name), out var b) ? b : null;
    private int? GetInt(string name) => int.TryParse(Get(name), out var i) ? i : null;
}

public sealed record LegacyData(string DataDir, List<LegacyApp> Apps, DateTime LastWriteTime, List<string> Warnings)
{
    public long TotalSeconds => Apps.Sum(a => a.TotalSeconds);
    public int FileCount => Apps.Sum(a => a.Files.Count);
}

/// <summary>
/// 旧バージョン (v1.x, .NET Framework 版) のデータを読む。旧データは書き換えない。
/// </summary>
public static class LegacyImporter
{
    public const string AppDataFileName = "appData.csv";

    /// <summary>旧バージョンの data フォルダらしいか</summary>
    public static bool IsLegacyDataDir(string dir) => File.Exists(Path.Combine(dir, AppDataFileName));

    /// <summary>MHTimer.exe のあるフォルダや data フォルダを指定されたとき、data フォルダを返す</summary>
    public static string? ResolveDataDir(string dir)
    {
        if (IsLegacyDataDir(dir)) return dir;
        var sub = Path.Combine(dir, "data");
        return IsLegacyDataDir(sub) ? sub : null;
    }

    public static LegacyData Read(string dataDir)
    {
        var warnings = new List<string>();
        var apps = new List<LegacyApp>();
        var appDataPath = Path.Combine(dataDir, AppDataFileName);
        var (text, _) = TextFile.Read(appDataPath);

        // 旧バージョンはカンマで単純に区切って書いていたので、こちらも単純に区切って読む
        var lines = text.Split(["\r\n", "\n"], StringSplitOptions.None);
        foreach (var line in lines.Skip(1))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var f = line.Split(',');
            var process = f[0].Trim();
            if (process.Length == 0) continue;
            if (f.Length != 8) warnings.Add($"{process}: 列の数が想定と違います。一部の設定が正しく読めていない可能性があります");
            if (apps.Any(a => string.Equals(a.Process, process, StringComparison.OrdinalIgnoreCase))) continue;

            var iconPath = Path.Combine(dataDir, "icons", process + ".png");
            apps.Add(new LegacyApp(
                process,
                ParseDuration(Field(f, 2)),
                bool.TryParse(Field(f, 4), out var toggl) && toggl,
                Field(f, 5),
                Field(f, 6),
                AppDefinition.ParseExtensions(Field(f, 7)),
                File.Exists(iconPath) ? iconPath : null,
                ReadFiles(Path.Combine(dataDir, "fileData", process + "_files.csv"), warnings)));
        }

        return new LegacyData(dataDir, apps, File.GetLastWriteTime(appDataPath), warnings);
    }

    private static List<LegacyFile> ReadFiles(string path, List<string> warnings)
    {
        var files = new List<LegacyFile>();
        if (!File.Exists(path)) return files;

        var (text, _) = TextFile.Read(path);
        foreach (var line in text.Split(["\r\n", "\n"], StringSplitOptions.None).Skip(1))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            // ファイル名にカンマが含まれていても読めるよう、最後のカンマで区切る
            var comma = line.LastIndexOf(',');
            if (comma <= 0)
            {
                warnings.Add($"{Path.GetFileName(path)}: 読めない行を飛ばしました ({line})");
                continue;
            }
            var name = line[..comma].Trim();
            var seconds = ParseDuration(line[(comma + 1)..]);
            if (name.Length == 0 || seconds <= 0) continue;

            var existing = files.FindIndex(x => x.Name == name);
            if (existing >= 0) files[existing] = files[existing] with { Seconds = files[existing].Seconds + seconds };
            else files.Add(new LegacyFile(name, seconds));
        }
        return files;
    }

    /// <summary>旧形式の "時:分:秒" (時は24を超える) を秒にする。"-" は0。</summary>
    public static long ParseDuration(string text)
    {
        text = text.Trim();
        if (text is "" or "-") return 0;
        return TimeText.TryParseSeconds(text, out var s) ? s : 0;
    }

    private static string Field(string[] f, int i) => i < f.Length ? f[i].Trim() : "";

    public static LegacySettings? ReadUserConfig(string path)
    {
        try
        {
            var doc = XDocument.Load(path);
            var values = doc.Descendants("setting")
                .Where(e => e.Attribute("name") != null)
                .GroupBy(e => (string)e.Attribute("name")!)
                .ToDictionary(g => g.Key, g => g.Last().Element("value")?.Value ?? "");
            return new LegacySettings(values);
        }
        catch (Exception ex) when (ex is IOException or System.Xml.XmlException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>
    /// 旧バージョンの user.config を探す。
    /// .NET Framework の設定は %LOCALAPPDATA%\(会社名)\MHTimer.exe_Url_(ハッシュ)\(バージョン)\user.config にある。
    /// </summary>
    public static string? FindUserConfig(string localAppData)
    {
        try
        {
            return Directory.EnumerateDirectories(localAppData)
                .SelectMany(d => SafeEnumerateDirectories(d, "MHTimer.exe_*"))
                .SelectMany(d => SafeEnumerateDirectories(d, "*"))
                .Select(d => Path.Combine(d, "user.config"))
                .Where(File.Exists)
                .OrderByDescending(File.GetLastWriteTime)
                .FirstOrDefault();
        }
        catch (IOException)
        {
            return null;
        }
    }

    private static IEnumerable<string> SafeEnumerateDirectories(string dir, string pattern)
    {
        try
        {
            return Directory.GetDirectories(dir, pattern);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }
}
