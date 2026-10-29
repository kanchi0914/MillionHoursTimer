namespace MillionHoursTimer.Core.Toggl;

/// <summary>Toggl のアカウント情報の保持と、送信待ちの作業の送信</summary>
public sealed class TogglService(TogglClient client, TogglQueue queue, ISecretStore secrets, ILog log)
{
    private readonly SemaphoreSlim _flushGate = new(1, 1);
    private string? _apiKey = secrets.Load();
    private long _workspaceId;
    private Dictionary<string, long> _projects = new(StringComparer.Ordinal);
    private List<string> _tags = [];

    public bool HasApiKey => !string.IsNullOrEmpty(_apiKey);
    public bool IsConnected => _workspaceId != 0;
    public string UserEmail { get; private set; } = "";
    public string LastError { get; private set; } = "";
    public int PendingCount { get; private set; }

    public IReadOnlyList<string> ProjectNames => [.. _projects.Keys.Order()];
    public IReadOnlyList<string> TagNames => _tags;

    public event Action? StateChanged;

    /// <summary>
    /// アカウント情報を読み込む。apiKey を渡した場合は、認証に成功したときだけ保存する (失敗すると例外)。
    /// </summary>
    public async Task ConnectAsync(string? apiKey = null, CancellationToken ct = default)
    {
        var key = apiKey ?? _apiKey;
        if (string.IsNullOrEmpty(key)) return;

        try
        {
            var me = await client.GetMeAsync(key, ct);
            var projects = await client.GetProjectsAsync(key, me.DefaultWorkspaceId, ct);
            var tags = await client.GetTagsAsync(key, me.DefaultWorkspaceId, ct);

            if (apiKey != null)
            {
                secrets.Save(apiKey);
                _apiKey = apiKey;
            }
            UserEmail = me.Email;
            _workspaceId = me.DefaultWorkspaceId;
            _projects = projects.GroupBy(p => p.Name).ToDictionary(g => g.Key, g => g.First().Id, StringComparer.Ordinal);
            _tags = tags.Select(t => t.Name).Distinct().Order().ToList();
            LastError = "";
        }
        catch (TogglApiException ex)
        {
            LastError = ex.Message;
            if (apiKey != null) throw;
            log.Warn($"Toggl に接続できませんでした: {ex.Message}");
        }
        finally
        {
            StateChanged?.Invoke();
        }
    }

    public void ClearApiKey()
    {
        secrets.Save(null);
        _apiKey = null;
        _workspaceId = 0;
        UserEmail = "";
        _projects.Clear();
        _tags.Clear();
        StateChanged?.Invoke();
    }

    public void Enqueue(TogglEntry entry)
    {
        try
        {
            queue.Enqueue(entry);
        }
        catch (IOException ex)
        {
            log.Error("Toggl の送信待ちを保存できませんでした", ex);
        }
    }

    /// <summary>送信待ちの作業を送る。失敗したものは次回に再送する。</summary>
    public async Task FlushAsync(CancellationToken ct = default)
    {
        if (!await _flushGate.WaitAsync(0, ct)) return;
        try
        {
            var pending = queue.LoadPending();
            PendingCount = pending.Count;
            if (pending.Count == 0 || !HasApiKey) return;
            if (!IsConnected) await ConnectAsync(ct: ct);
            if (!IsConnected) return;

            foreach (var entry in pending)
            {
                try
                {
                    long? projectId = null;
                    if (entry.Project.Length > 0)
                    {
                        if (_projects.TryGetValue(entry.Project, out var id)) projectId = id;
                        else log.Warn($"Toggl のプロジェクト「{entry.Project}」が見つからないため、プロジェクトなしで送信します");
                    }
                    string[]? tags = entry.Tag.Length > 0 ? [entry.Tag] : null;

                    var togglId = await client.CreateTimeEntryAsync(_apiKey!, _workspaceId, entry.Start, entry.Seconds,
                        entry.Description, projectId, tags, ct);
                    queue.MarkSent(entry.SessionId, togglId);
                    PendingCount--;
                    LastError = "";
                }
                catch (TogglApiException ex) when (ex.IsPermanent)
                {
                    log.Error($"Toggl への送信に失敗しました (再送しません): {entry}", ex);
                    queue.MarkFailed(entry.SessionId, ex.Message);
                    PendingCount--;
                    LastError = ex.Message;
                }
                catch (TogglApiException ex)
                {
                    log.Warn($"Toggl への送信に失敗しました (あとで再送します): {ex.Message}");
                    LastError = ex.Message;
                    break;
                }
            }
        }
        catch (IOException ex)
        {
            log.Error("Toggl の送信状況を読み書きできませんでした", ex);
        }
        finally
        {
            _flushGate.Release();
            StateChanged?.Invoke();
        }
    }
}
