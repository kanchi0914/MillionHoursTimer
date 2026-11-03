using System.Text;
using MillionHoursTimer.Core.Csv;
using MillionHoursTimer.Core.Model;

namespace MillionHoursTimer.Core.Storage;

/// <summary>
/// 作業記録 (records/apps/*.csv, records/files/*.csv) の読み書き。
/// 記録は月ごとのファイル (2026-10.csv) に追記し、移行した累計は legacy.csv に置く。
/// 手で編集されたファイルも読めるよう、列は名前で参照する。
/// </summary>
public sealed class RecordStore
{
    public const string LegacyFileKey = "legacy";

    private static readonly string[] AppColumns = ["start", "end", "seconds", "process", "app_name", "session_id"];
    private static readonly string[] FileColumns = ["start", "end", "seconds", "process", "app_name", "file", "session_id"];

    private readonly string _appDir;
    private readonly string _fileDir;

    public RecordStore(string appDir, string fileDir)
    {
        _appDir = appDir;
        _fileDir = fileDir;
    }

    public RecordStore(DataPaths paths) : this(paths.AppRecordsDir, paths.FileRecordsDir)
    {
    }

    public string DirectoryOf(RecordKind kind) => kind == RecordKind.App ? _appDir : _fileDir;

    private static string[] ColumnsOf(RecordKind kind) => kind == RecordKind.App ? AppColumns : FileColumns;

    public static string FileKeyOf(SessionRecord r) => r.Start is { } s ? s.ToString("yyyy-MM") : LegacyFileKey;

    public string PathOf(RecordKind kind, string fileKey) => Path.Combine(DirectoryOf(kind), fileKey + ".csv");

    /// <summary>
    /// 全ての記録を読む。読めない行は warnings に理由を入れて飛ばす。
    /// 開けないファイルは、strict なら <see cref="IOException"/> を投げ、そうでなければ warnings に入れて飛ばす。
    /// </summary>
    public List<SessionRecord> LoadAll(RecordKind kind, List<string>? warnings = null, bool strict = false)
    {
        var result = new List<SessionRecord>();
        var dir = DirectoryOf(kind);
        if (!Directory.Exists(dir)) return result;

        foreach (var path in Directory.EnumerateFiles(dir, "*.csv").Order(StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                var (text, _) = TextFile.Read(path);
                foreach (var entry in ParseEntries(text, kind))
                {
                    if (entry.Record != null) result.Add(entry.Record);
                    else warnings?.Add($"{Path.GetFileName(path)}: 読み込めない行を飛ばしました ({CsvFormat.FormatRow(entry.Mapped)})");
                }
            }
            catch (IOException ex) when (!strict)
            {
                warnings?.Add($"{Path.GetFileName(path)}: 読み込めませんでした ({ex.Message})");
            }
        }
        return result;
    }

    /// <summary>
    /// 記録を追記する。ファイルが Excel などで開かれていると <see cref="IOException"/> になるので、呼び出し側で再試行すること。
    /// </summary>
    public void Append(RecordKind kind, IReadOnlyCollection<SessionRecord> records)
    {
        foreach (var group in records.GroupBy(FileKeyOf))
        {
            AppendToFile(kind, PathOf(kind, group.Key), group.ToList());
        }
    }

    private void AppendToFile(RecordKind kind, string path, List<SessionRecord> records)
    {
        var columns = ColumnsOf(kind);
        var lines = new StringBuilder();
        foreach (var r in records) lines.Append(FormatRecord(kind, r)).Append("\r\n");

        if (!File.Exists(path))
        {
            TextFile.WriteAtomic(path, CsvFormat.FormatRow(columns) + "\r\n" + lines);
            return;
        }

        var (text, isUtf8) = TextFile.Read(path);
        var table = CsvTable.Parse(text);
        if (!isUtf8 || !table.HeaderEquals(columns))
        {
            // Excel で Shift-JIS に変わった・列が並び替えられた などの場合は、形式を揃えて書き直す
            TextFile.WriteAtomic(path, Serialize(kind, ParseEntries(text, kind)) + lines);
            return;
        }

        var prefix = text.Length > 0 && !text.EndsWith('\n') ? "\r\n" : "";
        using var fs = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read);
        using var writer = new StreamWriter(fs, TextFile.Utf8NoBom);
        writer.Write(prefix);
        writer.Write(lines);
    }

    /// <summary>
    /// 全ての記録に変換をかけて書き直す。transform が null を返した行は削除する。
    /// 読めない行はそのまま残す。変更された行数を返す。
    /// </summary>
    public int Rewrite(RecordKind kind, Func<SessionRecord, SessionRecord?> transform)
    {
        var dir = DirectoryOf(kind);
        if (!Directory.Exists(dir)) return 0;

        var changed = 0;
        foreach (var path in Directory.EnumerateFiles(dir, "*.csv"))
        {
            var (text, _) = TextFile.Read(path);
            var entries = ParseEntries(text, kind);
            var fileChanged = 0;
            var updated = new List<Entry>(entries.Count);
            foreach (var entry in entries)
            {
                if (entry.Record == null)
                {
                    updated.Add(entry);
                    continue;
                }
                var next = transform(entry.Record);
                if (!Equals(next, entry.Record)) fileChanged++;
                if (next != null) updated.Add(new Entry(next, entry.Mapped));
            }

            if (fileChanged > 0)
            {
                TextFile.WriteAtomic(path, Serialize(kind, updated));
                changed += fileChanged;
            }
        }
        return changed;
    }

    public static string FormatRecord(RecordKind kind, SessionRecord r)
    {
        var start = r.Start is { } s ? TimeText.Format(s) : "";
        var end = r.End is { } e ? TimeText.Format(e) : "";
        return kind == RecordKind.App
            ? CsvFormat.FormatRow([start, end, r.Seconds.ToString(), r.Process, r.AppName, r.SessionId])
            : CsvFormat.FormatRow([start, end, r.Seconds.ToString(), r.Process, r.AppName, r.File, r.SessionId]);
    }

    /// <summary>1行分。読めなかった行は Record が null で、Mapped に列を揃えた生の値を持つ。</summary>
    private sealed record Entry(SessionRecord? Record, string[] Mapped);

    private static List<Entry> ParseEntries(string text, RecordKind kind)
    {
        var table = CsvTable.Parse(text);
        var columns = ColumnsOf(kind);
        var entries = new List<Entry>(table.Rows.Count);
        foreach (var row in table.Rows)
        {
            var mapped = columns.Select(c => table.Get(row, c)).ToArray();
            entries.Add(new Entry(TryParse(table, row, kind), mapped));
        }
        return entries;
    }

    private static SessionRecord? TryParse(CsvTable table, string[] row, RecordKind kind)
    {
        var process = table.Get(row, "process");
        if (process.Length == 0) return null;

        DateTime? start = TimeText.TryParse(table.Get(row, "start"), out var s) ? s : null;
        DateTime? end = TimeText.TryParse(table.Get(row, "end"), out var e) ? e : null;
        long seconds;
        if (!TimeText.TryParseSeconds(table.Get(row, "seconds"), out seconds))
        {
            if (start is null || end is null || end < start) return null;
            seconds = (long)Math.Round((end.Value - start.Value).TotalSeconds);
        }

        var file = kind == RecordKind.File ? table.Get(row, "file") : "";
        if (kind == RecordKind.File && file.Length == 0) return null;

        return new SessionRecord
        {
            Start = start,
            End = end,
            Seconds = seconds,
            Process = process,
            AppName = table.Get(row, "app_name"),
            File = file,
            SessionId = table.Get(row, "session_id"),
        };
    }

    private static string Serialize(RecordKind kind, IEnumerable<Entry> entries)
    {
        var sb = new StringBuilder();
        sb.Append(CsvFormat.FormatRow(ColumnsOf(kind))).Append("\r\n");
        foreach (var entry in entries)
        {
            sb.Append(entry.Record != null ? FormatRecord(kind, entry.Record) : CsvFormat.FormatRow(entry.Mapped)).Append("\r\n");
        }
        return sb.ToString();
    }
}
