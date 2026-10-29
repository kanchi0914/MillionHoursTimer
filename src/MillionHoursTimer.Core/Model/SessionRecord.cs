namespace MillionHoursTimer.Core.Model;

public enum RecordKind
{
    /// <summary>アプリ単位の記録</summary>
    App,

    /// <summary>ファイル単位の記録</summary>
    File,
}

/// <summary>
/// 作業記録の1行。合計時間は <see cref="Seconds"/> で計算し、日付の振り分けには <see cref="Start"/> を使う。
/// 旧バージョンから移行した累計は Start/End を持たない。
/// </summary>
public sealed record SessionRecord
{
    public DateTime? Start { get; init; }
    public DateTime? End { get; init; }

    /// <summary>実際に計測された秒数 (End - Start より短いことがある)</summary>
    public long Seconds { get; init; }

    public string Process { get; init; } = "";

    /// <summary>記録した時点での表示名</summary>
    public string AppName { get; init; } = "";

    /// <summary>ファイル単位の記録のときのファイル名</summary>
    public string File { get; init; } = "";

    /// <summary>同じ作業 (日付をまたいで分割された行など) を表す ID</summary>
    public string SessionId { get; init; } = "";

    public DateOnly? Day => Start is { } s ? DateOnly.FromDateTime(s) : null;
}
