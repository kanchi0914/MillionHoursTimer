using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

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

public sealed record TogglNamedItem(long Id, string Name);

/// <summary>記録を送る先。Toggl 2.0 の API キーでは調べられないので、ログイン後の URL から読み取る</summary>
public sealed partial record TogglWorkspace(long OrganizationId, long WorkspaceId)
{
    /// <summary>https://focus.toggl.com/{organization_id}/workspaces/{workspace_id}/... の形の URL を読む</summary>
    public static TogglWorkspace? FromUrl(string url)
    {
        var m = UrlPattern().Match(url.Trim());
        return m.Success && long.TryParse(m.Groups[1].Value, out var org) && long.TryParse(m.Groups[2].Value, out var ws)
            ? new TogglWorkspace(org, ws)
            : null;
    }

    [GeneratedRegex(@"^(?:https?://)?[^/]*toggl\.com/(\d+)/workspaces/(\d+)(?:[/?#]|$)", RegexOptions.IgnoreCase)]
    private static partial Regex UrlPattern();
}

/// <summary>Toggl 2.0 API の必要な部分だけを呼ぶクライアント</summary>
public sealed class TogglClient(HttpClient http)
{
    public static readonly Uri BaseUri = new("https://focus.toggl.com/api/");

    private const int PageSize = 100;

    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public Task<List<TogglNamedItem>> GetProjectsAsync(string token, TogglWorkspace ws, CancellationToken ct = default) =>
        GetAllPagesAsync($"organizations/{ws.OrganizationId}/workspaces/{ws.WorkspaceId}/projects?archived=false", token, ct);

    public Task<List<TogglNamedItem>> GetTagsAsync(string token, TogglWorkspace ws, CancellationToken ct = default) =>
        GetAllPagesAsync($"workspaces/{ws.WorkspaceId}/tags?archived=false", token, ct);

    /// <returns>作成された time entry の ID</returns>
    public async Task<long> CreateTimeEntryAsync(string token, TogglWorkspace ws, DateTime startLocal, long seconds,
        string description, long? projectId, IReadOnlyList<long>? tagIds, CancellationToken ct = default)
    {
        var body = new TimeEntryRequest
        {
            Type = "activity",
            Description = description,
            ProjectId = projectId,
            TagIds = tagIds is { Count: > 0 } ? tagIds : null,
            Start = startLocal.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ"),
            Duration = seconds,
        };
        var created = await SendAsync<NamedResponse>(HttpMethod.Post,
            $"organizations/{ws.OrganizationId}/workspaces/{ws.WorkspaceId}/time-entries", token, body, ct);
        return created.Id;
    }

    private async Task<List<TogglNamedItem>> GetAllPagesAsync(string path, string token, CancellationToken ct)
    {
        var result = new List<TogglNamedItem>();
        for (var page = 1; page <= 50; page++)
        {
            var res = await SendAsync<PageResponse>(HttpMethod.Get, $"{path}&per_page={PageSize}&page={page}", token, null, ct);
            var items = res.Data ?? [];
            result.AddRange(items.Select(i => new TogglNamedItem(i.Id, i.Name ?? "")));
            if (items.Count < PageSize || result.Count >= res.Total) break;
        }
        return result;
    }

    private async Task<T> SendAsync<T>(HttpMethod method, string path, string token, object? body, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(method, new Uri(BaseUri, path));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
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
                    $"Toggl がエラーを返しました ({(int)response.StatusCode}): {ErrorDescription(text)}");
            }
            return (await response.Content.ReadFromJsonAsync<T>(Json, ct))!;
        }
    }

    /// <summary>エラーの本文は {"error": "...", "error_description": "..."} の形。読めなければそのまま返す</summary>
    private static string ErrorDescription(string text)
    {
        try
        {
            var error = JsonSerializer.Deserialize<ErrorResponse>(text, Json);
            if (!string.IsNullOrEmpty(error?.ErrorDescription)) return error.ErrorDescription;
        }
        catch (JsonException)
        {
        }
        return text.Trim();
    }

    private sealed class PageResponse
    {
        public List<NamedResponse>? Data { get; set; }
        public int Total { get; set; }
    }

    private sealed class NamedResponse
    {
        public long Id { get; set; }
        public string? Name { get; set; }
    }

    private sealed class ErrorResponse
    {
        public string? ErrorDescription { get; set; }
    }

    private sealed class TimeEntryRequest
    {
        public string Type { get; set; } = "";
        public string Description { get; set; } = "";
        public long? ProjectId { get; set; }
        public IReadOnlyList<long>? TagIds { get; set; }
        public string Start { get; set; } = "";
        public long Duration { get; set; }
    }
}
