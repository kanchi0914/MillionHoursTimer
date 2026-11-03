using System.Text;
using MillionHoursTimer.Core.Csv;

namespace MillionHoursTimer.Core.Storage;

/// <summary>ロックしたファイル別の記録 (file_locks.csv)。ロックした記録は自動削除・「全て削除」の対象にしない。</summary>
public static class FileLockStore
{
    private static readonly string[] Columns = ["process", "file"];

    /// <returns>プロセス名 → ロックしたファイル名</returns>
    public static Dictionary<string, HashSet<string>> Load(string path, List<string>? warnings = null)
    {
        var locks = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        if (!File.Exists(path)) return locks;

        var (text, _) = TextFile.Read(path);
        var table = CsvTable.Parse(text);
        foreach (var row in table.Rows)
        {
            var process = table.Get(row, "process");
            var file = table.Get(row, "file");
            if (process.Length == 0 || file.Length == 0)
            {
                warnings?.Add($"file_locks.csv: process か file が空の行を飛ばしました ({CsvFormat.FormatRow(row)})");
                continue;
            }
            if (!locks.TryGetValue(process, out var files))
            {
                files = new HashSet<string>(StringComparer.Ordinal);
                locks[process] = files;
            }
            files.Add(file);
        }
        return locks;
    }

    public static void Save(string path, IReadOnlyDictionary<string, HashSet<string>> locks)
    {
        var sb = new StringBuilder();
        sb.Append(CsvFormat.FormatRow(Columns)).Append("\r\n");
        foreach (var (process, files) in locks.OrderBy(p => p.Key, StringComparer.OrdinalIgnoreCase))
        {
            foreach (var file in files.Order(StringComparer.Ordinal)) sb.Append(CsvFormat.FormatRow([process, file])).Append("\r\n");
        }
        TextFile.WriteAtomic(path, sb.ToString());
    }
}
