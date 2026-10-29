namespace MillionHoursTimer.Core.Csv;

/// <summary>
/// 1行目をヘッダーとして扱う CSV。列は名前で参照するので、列の並び替えにも耐える。
/// </summary>
public sealed class CsvTable
{
    private readonly Dictionary<string, int> _index = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<string> Header { get; }
    public IReadOnlyList<string[]> Rows { get; }

    private CsvTable(string[] header, List<string[]> rows)
    {
        Header = header;
        Rows = rows;
        for (var i = 0; i < header.Length; i++) _index.TryAdd(header[i], i);
    }

    public static CsvTable Parse(string text)
    {
        var all = CsvFormat.Parse(text);
        var header = all.Count > 0 ? all[0].Select(h => h.Trim()).ToArray() : [];
        return new CsvTable(header, all.Skip(1).ToList());
    }

    public bool HasColumn(string column) => _index.ContainsKey(column);

    public string Get(string[] row, string column) =>
        _index.TryGetValue(column, out var i) && i < row.Length ? row[i].Trim() : "";

    /// <summary>ヘッダーが指定した列と同じ並びかどうか</summary>
    public bool HeaderEquals(IReadOnlyList<string> columns) =>
        Header.Count == columns.Count
        && Header.Zip(columns).All(p => string.Equals(p.First, p.Second, StringComparison.OrdinalIgnoreCase));
}
