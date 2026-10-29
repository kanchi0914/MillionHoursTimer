namespace MillionHoursTimer.Core.Model;

/// <summary>計測対象として登録されたアプリ</summary>
public sealed class AppDefinition
{
    /// <summary>プロセス名 (拡張子なしの実行ファイル名)。アプリを識別するキー。</summary>
    public required string Process { get; init; }

    /// <summary>画面に表示する名前</summary>
    public string Name { get; set; } = "";

    /// <summary>登録時の実行ファイルのパス (アイコンの再取得に使う)</summary>
    public string ExePath { get; set; } = "";

    /// <summary>ファイル別に記録するときの拡張子 (".png" など)</summary>
    public List<string> Extensions { get; set; } = [];

    public bool TogglEnabled { get; set; }
    public string TogglProject { get; set; } = "";
    public string TogglTag { get; set; } = "";

    public string DisplayName => string.IsNullOrWhiteSpace(Name) ? Process : Name;

    public AppDefinition Clone() => new()
    {
        Process = Process,
        Name = Name,
        ExePath = ExePath,
        Extensions = [.. Extensions],
        TogglEnabled = TogglEnabled,
        TogglProject = TogglProject,
        TogglTag = TogglTag,
    };

    /// <summary>"/" 区切りの拡張子文字列を解釈する ("." を含まないものは無視)</summary>
    public static List<string> ParseExtensions(string? text) =>
        (text ?? "").Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(e => e.Contains('.'))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    public static string FormatExtensions(IEnumerable<string> extensions) => string.Join("/", extensions);
}
