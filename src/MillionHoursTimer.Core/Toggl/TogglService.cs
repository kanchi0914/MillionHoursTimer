using System.Text.Json;

namespace MillionHoursTimer.Core.Toggl;

/// <summary>Toggl のアカウント情報の保持と、送信待ちの作業の送信</summary>
public sealed class TogglService(TogglClient client, TogglQueue queue, ISecretStore secrets, ILog log)
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

    private readonly SemaphoreSlim _flushGate = new(1, 1);
    private StoredAccount? _account = LoadAccount(secrets, log);
    private bool _connected;
    private Dictionary<string, long> _projects = new(StringComparer.Ordinal);
    private Dictionary<string, long> _tags = new(StringComparer.Ordinal);

    public bool HasApiKey => _account != null;
    public bool IsConnected => _connected;
    public TogglWorkspace? Workspace => _account is { } a ? new TogglWorkspace(a.OrganizationId, a.WorkspaceId) : null;
    public string LastError { get; private set; } = "";
    public int PendingCount { get; private set; }

    public IReadOnlyList<string> ProjectNames => [.. _projects.Keys.Order()];
    public IReadOnlyList<string> TagNames => [.. _tags.Keys.Order()];

    public event Action? StateChanged;

    /// <summary>
    /// アカウント情報を読み込む。apiKey か workspace を渡した場合は、保存済みのものと組み合わせて認証し、
    /// 成功したときだけ保存する (失敗すると例外)。
    /// </summary>
    public async Task ConnectAsync(string? apiKey = null, TogglWorkspace? workspace = null, CancellationToken ct = default)
    {
        var key = apiKey ?? _account?.ApiKey;
        var ws = workspace ?? Workspace;
        if (string.IsNullOrEmpty(key) || ws == null) return;
        var isNew = apiKey != null || workspace != null;

        try
        {
            var projects = await client.GetProjectsAsync(key, ws, ct);
            var tags = await client.GetTagsAsync(key, ws, ct);

            if (isNew)
            {
                var account = new StoredAccount(key, ws.OrganizationId, ws.WorkspaceId);
                secrets.Save(JsonSerializer.Serialize(account, Json));
                _account = account;
            }
            _connected = true;
            _projects = ToLookup(projects);
            _tags = ToLookup(tags);
            LastError = "";
        }
        catch (TogglApiException ex)
        {
            LastError = ex.Message;
            if (isNew) throw;
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
        _account = null;
        _connected = false;
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
                    long[]? tagIds = null;
                    if (entry.Tag.Length > 0)
                    {
                        if (_tags.TryGetValue(entry.Tag, out var id)) tagIds = [id];
                        else log.Warn($"Toggl のタグ「{entry.Tag}」が見つからないため、タグなしで送信します");
                    }

                    var togglId = await client.CreateTimeEntryAsync(_account!.ApiKey, Workspace!, entry.Start, entry.Seconds,
                        entry.Description, projectId, tagIds, ct);
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

    private static Dictionary<string, long> ToLookup(List<TogglNamedItem> items) =>
        items.GroupBy(i => i.Name).ToDictionary(g => g.Key, g => g.First().Id, StringComparer.Ordinal);

    private static StoredAccount? LoadAccount(ISecretStore secrets, ILog log)
    {
        var text = secrets.Load();
        if (string.IsNullOrEmpty(text)) return null;
        try
        {
            if (JsonSerializer.Deserialize<StoredAccount>(text, Json) is { ApiKey.Length: > 0, OrganizationId: > 0, WorkspaceId: > 0 } account)
            {
                return account;
            }
        }
        catch (JsonException)
        {
        }
        // v2.0 のテスト版までは Toggl Track の API キーをそのまま保存していた。Toggl 2.0 では使えない
        log.Warn("保存されている Toggl の API キーは使えない形式です。設定画面から Toggl 2.0 の API キーを設定し直してください");
        return null;
    }

    private sealed record StoredAccount(string ApiKey, long OrganizationId, long WorkspaceId);
}
