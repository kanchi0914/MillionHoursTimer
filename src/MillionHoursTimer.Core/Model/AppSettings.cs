namespace MillionHoursTimer.Core.Model;

public enum CountMode
{
    /// <summary>起動しているアプリを全て計測</summary>
    All,

    /// <summary>最小化していないアプリのみ計測</summary>
    NotMinimized,

    /// <summary>アクティブウィンドウのアプリのみ計測</summary>
    ActiveOnly,
}

/// <summary>settings.json に保存するユーザー設定。既定値は旧バージョンに合わせている。</summary>
public sealed class AppSettings
{
    public CountMode CountMode { get; set; } = CountMode.NotMinimized;

    /// <summary>計測間隔 (秒)</summary>
    public int CountIntervalSeconds { get; set; } = 1;

    /// <summary>この秒数だけ PC の操作がなければ計測を止める。0 で無効。</summary>
    public int IdleTimeoutSeconds { get; set; } = 60;

    /// <summary>これより短い作業は記録しない (秒)</summary>
    public int MinRecordSeconds { get; set; }

    /// <summary>「ファイル名 - アプリ名」形式のタイトルからファイル名を取る</summary>
    public bool UseHyphenFileName { get; set; } = true;

    /// <summary>取得したファイル名をさらにスペースで区切る</summary>
    public bool SplitFileNameBySpace { get; set; } = true;

    /// <summary>ファイル名を表示するウィンドウは、最小化・非アクティブでもファイル別に記録する</summary>
    public bool IgnoreWindowStateForFiles { get; set; }

    /// <summary>これより短い作業は Toggl に送らない (秒)</summary>
    public int TogglMinSeconds { get; set; } = 30;

    public bool AutoStart { get; set; }

    /// <summary>旧バージョンからデータを取り込んだ記録</summary>
    public LegacyMigrationInfo? Migration { get; set; }

    /// <summary>「旧バージョンは使っていない」が選ばれたら、起動時に取り込みを案内しない</summary>
    public bool SkipLegacyPrompt { get; set; }

    public AppSettings Clone() => (AppSettings)MemberwiseClone();

    public void Normalize()
    {
        CountIntervalSeconds = Math.Clamp(CountIntervalSeconds, 1, 3600);
        IdleTimeoutSeconds = Math.Max(0, IdleTimeoutSeconds);
        MinRecordSeconds = Math.Max(0, MinRecordSeconds);
        TogglMinSeconds = Math.Max(0, TogglMinSeconds);
        if (!Enum.IsDefined(CountMode)) CountMode = CountMode.NotMinimized;
    }
}

public sealed class LegacyMigrationInfo
{
    public string SourceDir { get; set; } = "";
    public DateTime MigratedAt { get; set; }
}
