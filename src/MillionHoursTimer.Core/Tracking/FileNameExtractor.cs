using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace MillionHoursTimer.Core.Tracking;

/// <summary>ウィンドウタイトルからファイル名を取り出す (旧バージョンの判定方法を踏襲)</summary>
public static class FileNameExtractor
{
    private static readonly Regex HyphenPattern = new(@"(.+?)\s+[-–—]", RegexOptions.Compiled);
    private static readonly ConcurrentDictionary<string, Regex> PatternCache = new(StringComparer.OrdinalIgnoreCase);

    /// <param name="title">ウィンドウタイトル</param>
    /// <param name="extensions">".png" などの拡張子。先に並んでいるものを優先する</param>
    /// <param name="splitBySpace">取り出した名前をさらにスペースで区切る</param>
    /// <param name="useHyphen">拡張子で見つからなければ「ファイル名 - アプリ名」形式とみなす</param>
    public static string? Extract(string title, IReadOnlyList<string> extensions, bool splitBySpace, bool useHyphen)
    {
        if (string.IsNullOrWhiteSpace(title)) return null;

        foreach (var ext in extensions)
        {
            if (!ext.Contains('.')) continue;
            var pattern = PatternCache.GetOrAdd(ext, e => new Regex(@"\s*(.+" + Regex.Escape(e) + ")", RegexOptions.IgnoreCase));
            var match = pattern.Match(title);
            if (!match.Success) continue;

            if (splitBySpace)
            {
                foreach (var token in match.Groups[1].Value.Split(' '))
                {
                    var tokenMatch = pattern.Match(token);
                    if (tokenMatch.Success) return NullIfEmpty(tokenMatch.Value);
                }
            }
            return NullIfEmpty(match.Value);
        }

        if (useHyphen)
        {
            var match = HyphenPattern.Match(title);
            if (match.Success)
            {
                var name = match.Groups[1].Value;
                return NullIfEmpty(splitBySpace ? name.Trim().Split(' ')[0] : name);
            }
        }
        return null;
    }

    private static string? NullIfEmpty(string s)
    {
        s = s.Trim();
        return s.Length == 0 ? null : s;
    }
}
