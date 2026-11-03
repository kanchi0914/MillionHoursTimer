using System.Text;
using MillionHoursTimer.Core.Csv;

namespace MillionHoursTimer.Core.Toggl;

/// <summary>Toggl に送る作業1件分</summary>
public sealed record TogglEntry(string SessionId, DateTime Start, long Seconds, string Description, string Project, string Tag);

/// <summary>
/// Toggl への送信状況 (toggl_log.csv)。追記のみで、同じ session_id の最後の行がその作業の状態になる。
/// pending のまま残っている作業は、次に送信できるときに再送する。
/// </summary>
public sealed class TogglQueue(string path)
{
    public const string Pending = "pending";
    public const string Sent = "sent";
    public const string Failed = "failed";

    private static readonly string[] Columns =
        ["logged_at", "session_id", "status", "start", "seconds", "description", "project", "tag", "toggl_id", "message"];

    private readonly object _gate = new();

    public void Enqueue(TogglEntry e) =>
        Append(e.SessionId, Pending, TimeText.Format(e.Start), e.Seconds.ToString(), e.Description, e.Project, e.Tag, "", "");

    public void MarkSent(string sessionId, long togglId) =>
        Append(sessionId, Sent, "", "", "", "", "", togglId.ToString(), "");

    public void MarkFailed(string sessionId, string message) =>
        Append(sessionId, Failed, "", "", "", "", "", "", message);

    public List<TogglEntry> LoadPending()
    {
        lock (_gate)
        {
            if (!File.Exists(path)) return [];
            var (text, _) = TextFile.Read(path);
            var table = CsvTable.Parse(text);

            var entries = new Dictionary<string, TogglEntry>();
            var status = new Dictionary<string, string>();
            foreach (var row in table.Rows)
            {
                var id = table.Get(row, "session_id");
                if (id.Length == 0) continue;
                var st = table.Get(row, "status");
                status[id] = st;
                if (st == Pending
                    && TimeText.TryParse(table.Get(row, "start"), out var start)
                    && TimeText.TryParseSeconds(table.Get(row, "seconds"), out var seconds))
                {
                    entries[id] = new TogglEntry(id, start, seconds, table.Get(row, "description"),
                        table.Get(row, "project"), table.Get(row, "tag"));
                }
            }
            return entries.Values.Where(e => status[e.SessionId] == Pending).OrderBy(e => e.Start).ToList();
        }
    }

    private void Append(params string[] fields)
    {
        lock (_gate)
        {
            var line = CsvFormat.FormatRow([TimeText.Format(DateTime.Now), .. fields]) + "\r\n";
            if (!File.Exists(path))
            {
                TextFile.WriteAtomic(path, CsvFormat.FormatRow(Columns) + "\r\n" + line);
                return;
            }
            File.AppendAllText(path, line, TextFile.Utf8NoBom);
        }
    }
}
