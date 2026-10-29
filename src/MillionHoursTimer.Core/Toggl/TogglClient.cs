using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MillionHoursTimer.Core.Toggl;

public sealed class TogglApiException(HttpStatusCode? statusCode, string message, Exception? inner = null)
    : Exception(message, inner)
{
    public HttpStatusCode? StatusCode { get; } = statusCode;

    /// <summary>再送しても成功しない種類のエラーか (認証エラーはキーを直せば通るので含めない)</summary>
    public bool IsPermanent => StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.NotFound
        or HttpStatusCode.UnprocessableEntity or HttpStatusCode.Conflict;

    public bool IsAuthError => StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden;
}

public sealed record TogglUser(string Email, long DefaultWorkspaceId);

public sealed record TogglNamedItem(long Id, string Name);

/// <summary>Toggl Track API v9 の必要な部分だけを呼ぶクライアント</summary>
public sealed class TogglClient(HttpClient http)
{
    public static readonly Uri BaseUri = new("https://api.track.toggl.com/api/v9/");

    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public async Task<TogglUser> GetMeAsync(string token, CancellationToken ct = default)
    {
        var me = await SendAsync<MeResponse>(HttpMethod.Get, "me", token, null, ct);
        return new TogglUser(me.Email ?? "", me.DefaultWorkspaceId);
    }

    public async Task<List<TogglNamedItem>> GetProjectsAsync(string token, long workspaceId, CancellationToken ct = default)
    {
        var result = new List<TogglNamedItem>();
        for (var page = 1; page <= 50; page++)
        {
            var items = await SendAsync<List<NamedResponse>>(HttpMethod.Get,
                $"workspaces/{workspaceId}/projects?active=true&per_page=200&page={page}", token, null, ct);
            result.AddRange(items.Select(i => new TogglNamedItem(i.Id, i.Name ?? "")));
            if (items.Count < 200) break;
        }
        return result;
    }

    public async Task<List<TogglNamedItem>> GetTagsAsync(string token, long workspaceId, CancellationToken ct = default)
    {
        var items = await SendAsync<List<NamedResponse>?>(HttpMethod.Get, $"workspaces/{workspaceId}/tags", token, null, ct);
        return (items ?? []).Select(i => new TogglNamedItem(i.Id, i.Name ?? "")).ToList();
    }

    /// <returns>作成された time entry の ID</returns>
    public async Task<long> CreateTimeEntryAsync(string token, long workspaceId, DateTime startLocal, long seconds,
        string description, long? projectId, IReadOnlyList<string>? tags, CancellationToken ct = default)
    {
        var start = startLocal.ToUniversalTime();
        var body = new TimeEntryRequest
        {
            CreatedWith = "MillionHoursTimer",
            Description = description,
            WorkspaceId = workspaceId,
            ProjectId = projectId,
            Tags = tags is { Count: > 0 } ? tags : null,
            Start = start.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            Stop = start.AddSeconds(seconds).ToString("yyyy-MM-ddTHH:mm:ssZ"),
            Duration = seconds,
        };
        var created = await SendAsync<NamedResponse>(HttpMethod.Post, $"workspaces/{workspaceId}/time_entries", token, body, ct);
        return created.Id;
    }

    private async Task<T> SendAsync<T>(HttpMethod method, string path, string token, object? body, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(method, new Uri(BaseUri, path));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic",
            Convert.ToBase64String(Encoding.ASCII.GetBytes($"{token}:api_token")));
        if (body != null) request.Content = JsonContent.Create(body, body.GetType(), options: Json);

        HttpResponseMessage response;
        try
        {
            response = await http.SendAsync(request, ct);
        }
        catch (HttpRequestException ex)
        {
            throw new TogglApiException(null, $"Toggl に接続できませんでした: {ex.Message}", ex);
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            throw new TogglApiException(null, "Toggl への接続がタイムアウトしました", ex);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                var text = await response.Content.ReadAsStringAsync(ct);
                throw new TogglApiException(response.StatusCode,
                    $"Toggl がエラーを返しました ({(int)response.StatusCode}): {text.Trim()}");
            }
            return (await response.Content.ReadFromJsonAsync<T>(Json, ct))!;
        }
    }

    private sealed class MeResponse
    {
        public string? Email { get; set; }
        public long DefaultWorkspaceId { get; set; }
    }

    private sealed class NamedResponse
    {
        public long Id { get; set; }
        public string? Name { get; set; }
    }

    private sealed class TimeEntryRequest
    {
        public string CreatedWith { get; set; } = "";
        public string Description { get; set; } = "";
        public long WorkspaceId { get; set; }
        public long? ProjectId { get; set; }
        public IReadOnlyList<string>? Tags { get; set; }
        public string Start { get; set; } = "";
        public string Stop { get; set; } = "";
        public long Duration { get; set; }
    }
}
