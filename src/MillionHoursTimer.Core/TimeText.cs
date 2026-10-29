using System.Globalization;

namespace MillionHoursTimer.Core;

/// <summary>日時・秒数の文字列変換</summary>
public static class TimeText
{
    public const string DateTimeFormat = "yyyy-MM-dd HH:mm:ss";

    // Excel で上書き保存したときなどの揺れも受け付ける
    private static readonly string[] AcceptedFormats =
    [
        "yyyy-MM-dd HH:mm:ss",
        "yyyy-MM-dd H:mm:ss",
        "yyyy-MM-dd HH:mm",
        "yyyy-MM-dd H:mm",
        "yyyy-MM-ddTHH:mm:ss",
        "yyyy/MM/dd HH:mm:ss",
        "yyyy/M/d H:mm:ss",
        "yyyy/MM/dd HH:mm",
        "yyyy/M/d H:mm",
    ];

    private static readonly CultureInfo Japanese = CultureInfo.GetCultureInfo("ja-JP");

    public static string Format(DateTime value) => value.ToString(DateTimeFormat, CultureInfo.InvariantCulture);

    public static bool TryParse(string? text, out DateTime value)
    {
        value = default;
        if (string.IsNullOrWhiteSpace(text)) return false;
        text = text.Trim();
        return DateTime.TryParseExact(text, AcceptedFormats, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out value)
            || DateTime.TryParse(text, Japanese, DateTimeStyles.AllowWhiteSpaces, out value);
    }

    /// <summary>"3600" のような秒数か、"1:00:00" のような 時:分:秒 を秒数として読む</summary>
    public static bool TryParseSeconds(string? text, out long seconds)
    {
        seconds = 0;
        if (string.IsNullOrWhiteSpace(text)) return false;
        text = text.Trim();
        if (long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out seconds)) return seconds >= 0;
        if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) && d >= 0)
        {
            seconds = (long)Math.Round(d);
            return true;
        }

        var parts = text.Split(':');
        if (parts.Length == 3
            && long.TryParse(parts[0], out var h) && int.TryParse(parts[1], out var m) && int.TryParse(parts[2], out var s)
            && h >= 0 && m is >= 0 and < 60 && s is >= 0 and < 60)
        {
            seconds = h * 3600 + m * 60 + s;
            return true;
        }
        return false;
    }

    /// <summary>画面表示用 (例: "  12時間 5分 3秒")。1秒未満は "-"</summary>
    public static string FormatDuration(long seconds)
    {
        if (seconds < 1) return "-";
        var h = seconds / 3600;
        var m = seconds % 3600 / 60;
        var s = seconds % 60;
        return $"{h,4}時間{m,2}分{s,2}秒";
    }

    /// <summary>短い表示用 (例: "12.5h", "35m")</summary>
    public static string FormatShort(long seconds)
    {
        if (seconds >= 3600) return $"{seconds / 3600.0:0.#}h";
        if (seconds >= 60) return $"{seconds / 60}m";
        return $"{seconds}s";
    }
}
