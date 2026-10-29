using System.Text;
using MillionHoursTimer.Core.Csv;
using MillionHoursTimer.Core.Model;

namespace MillionHoursTimer.Core.Storage;

/// <summary>登録アプリの一覧 (apps.csv)。行の順番が画面の表示順になる。</summary>
public static class AppListStore
{
    private static readonly string[] Columns =
        ["process", "app_name", "exe_path", "extensions", "toggl", "toggl_project", "toggl_tag"];

    public static List<AppDefinition> Load(string path, List<string>? warnings = null)
    {
        var apps = new List<AppDefinition>();
        if (!File.Exists(path)) return apps;

        var (text, _) = TextFile.Read(path);
        var table = CsvTable.Parse(text);
        foreach (var row in table.Rows)
        {
            var process = table.Get(row, "process");
            if (process.Length == 0)
            {
                warnings?.Add($"apps.csv: process が空の行を飛ばしました ({CsvFormat.FormatRow(row)})");
                continue;
            }
            if (apps.Any(a => string.Equals(a.Process, process, StringComparison.OrdinalIgnoreCase)))
            {
                warnings?.Add($"apps.csv: {process} が重複していたので2つ目以降を飛ばしました");
                continue;
            }

            apps.Add(new AppDefinition
            {
                Process = process,
                Name = table.Get(row, "app_name"),
                ExePath = table.Get(row, "exe_path"),
                Extensions = AppDefinition.ParseExtensions(table.Get(row, "extensions")),
                TogglEnabled = ParseBool(table.Get(row, "toggl")),
                TogglProject = table.Get(row, "toggl_project"),
                TogglTag = table.Get(row, "toggl_tag"),
            });
        }
        return apps;
    }

    public static void Save(string path, IEnumerable<AppDefinition> apps)
    {
        var sb = new StringBuilder();
        sb.Append(CsvFormat.FormatRow(Columns)).Append("\r\n");
        foreach (var a in apps)
        {
            sb.Append(CsvFormat.FormatRow(
            [
                a.Process, a.Name, a.ExePath, AppDefinition.FormatExtensions(a.Extensions),
                a.TogglEnabled ? "true" : "false", a.TogglProject, a.TogglTag,
            ])).Append("\r\n");
        }
        TextFile.WriteAtomic(path, sb.ToString());
    }

    private static bool ParseBool(string text) =>
        text.Equals("true", StringComparison.OrdinalIgnoreCase) || text == "1"
        || text.Equals("yes", StringComparison.OrdinalIgnoreCase) || text == "○";
}
