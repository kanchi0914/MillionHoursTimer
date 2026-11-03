using System.Net;
using System.Text;
using System.Text.Json;
using MillionHoursTimer.Core.Storage;
using MillionHoursTimer.Core.Toggl;

namespace MillionHoursTimer.Core.Tests;

public class TogglTests : IDisposable
{
    private readonly TempDir _dir = new();
    private readonly FakeToggl _api = new();
    private readonly MemorySecrets _secrets = new();

    public void Dispose() => _dir.Dispose();

    private TogglQueue Queue => new(_dir.Combine("toggl_log.csv"));

    private TogglService CreateService() =>
        new(new TogglClient(new HttpClient(_api)), Queue, _secrets, NullLog.Instance);

    private static TogglEntry Entry(string id, long seconds = 120, string project = "絵") =>
        new(id, new DateTime(2026, 10, 29, 21, 0, 0), seconds, "クリスタ", project, "作業");

    [Fact]
    public void QueueReportsOnlyEntriesWhoseLastStatusIsPending()
    {
        var queue = Queue;
        queue.Enqueue(Entry("a"));
        queue.Enqueue(Entry("b"));
        queue.Enqueue(Entry("c"));
        queue.MarkSent("a", 1);
        queue.MarkFailed("c", "bad request");

        var pending = Assert.Single(queue.LoadPending());
        Assert.Equal(Entry("b"), pending);
    }

    private const string SavedAccount = """{"api_key":"toggl_sk_token","organization_id":5,"workspace_id":42}""";

    [Theory]
    [InlineData("https://focus.toggl.com/21786528/workspaces/21785855/calendar", 21786528L, 21785855L)]
    [InlineData("focus.toggl.com/5/workspaces/42", 5L, 42L)]
    [InlineData("  https://focus.toggl.com/5/workspaces/42?view=week  ", 5L, 42L)]
    public void WorkspaceIsReadFromUrl(string url, long org, long ws) =>
        Assert.Equal(new TogglWorkspace(org, ws), TogglWorkspace.FromUrl(url));

    [Theory]
    [InlineData("https://track.toggl.com/profile")]
    [InlineData("https://focus.toggl.com/settings")]
    [InlineData("https://example.com/5/workspaces/42")]
    [InlineData("21785855")]
    public void UrlWithoutWorkspaceIsRejected(string url) => Assert.Null(TogglWorkspace.FromUrl(url));

    [Fact]
    public async Task ConnectSavesAccountOnlyWhenAuthenticationSucceeds()
    {
        var service = CreateService();
        var workspace = new TogglWorkspace(5, 42);
        _api.Unauthorized = true;
        await Assert.ThrowsAsync<TogglApiException>(() => service.ConnectAsync("wrong", workspace));
        Assert.Null(_secrets.Value);
        Assert.False(service.HasApiKey);

        _api.Unauthorized = false;
        await service.ConnectAsync("toggl_sk_token", workspace);
        Assert.Equal(SavedAccount, _secrets.Value);
        Assert.True(service.IsConnected);
        Assert.Equal(workspace, service.Workspace);
        Assert.Equal(["絵"], service.ProjectNames);
        Assert.Equal(["作業"], service.TagNames);
        Assert.Equal("Bearer toggl_sk_token", _api.LastAuthorization);
    }

    [Fact]
    public async Task AuthErrorMessageUsesErrorDescription()
    {
        var service = CreateService();
        _api.Unauthorized = true;
        var ex = await Assert.ThrowsAsync<TogglApiException>(() => service.ConnectAsync("wrong", new TogglWorkspace(5, 42)));
        Assert.True(ex.IsAuthError);
        Assert.EndsWith("(401): the provided API key is invalid", ex.Message);
    }

    [Fact]
    public void KeySavedByOlderVersionIsIgnored()
    {
        _secrets.Value = "0123456789abcdef0123456789abcdef";
        Assert.False(CreateService().HasApiKey);
    }

    [Fact]
    public async Task FlushSendsPendingEntriesWithProjectAndTag()
    {
        _secrets.Value = SavedAccount;
        var service = CreateService();
        service.Enqueue(Entry("a"));

        await service.FlushAsync();

        var body = Assert.Single(_api.CreatedEntries);
        Assert.Equal("activity", body.GetProperty("type").GetString());
        Assert.Equal(7, body.GetProperty("project_id").GetInt64());
        Assert.Equal(3, body.GetProperty("tag_ids")[0].GetInt64());
        Assert.Equal(120, body.GetProperty("duration").GetInt64());
        Assert.Equal("クリスタ", body.GetProperty("description").GetString());
        Assert.EndsWith("Z", body.GetProperty("start").GetString());
        Assert.Empty(Queue.LoadPending());
        Assert.Equal(0, service.PendingCount);
    }

    [Fact]
    public async Task ProjectsAndTagsAreReadFromAllPages()
    {
        _secrets.Value = SavedAccount;
        _api.ExtraProjects = 150;
        var service = CreateService();

        await service.ConnectAsync();

        Assert.Equal(151, service.ProjectNames.Count);
    }

    [Fact]
    public async Task ServerErrorsAreRetriedLaterButBadRequestsAreNot()
    {
        _secrets.Value = SavedAccount;
        var service = CreateService();
        service.Enqueue(Entry("a"));
        service.Enqueue(Entry("b", project: "存在しない"));

        _api.CreateStatus = HttpStatusCode.ServiceUnavailable;
        await service.FlushAsync();
        Assert.Equal(2, Queue.LoadPending().Count);

        _api.CreateStatus = HttpStatusCode.BadRequest;
        await service.FlushAsync();
        Assert.Empty(Queue.LoadPending());
        Assert.Contains("failed", File.ReadAllText(_dir.Combine("toggl_log.csv")));
    }

    [Fact]
    public async Task UnknownProjectAndTagAreSentWithoutThem()
    {
        _secrets.Value = SavedAccount;
        var service = CreateService();
        service.Enqueue(new TogglEntry("a", new DateTime(2026, 10, 29, 21, 0, 0), 120, "クリスタ", "存在しない", "存在しない"));

        await service.FlushAsync();

        var body = Assert.Single(_api.CreatedEntries);
        Assert.False(body.TryGetProperty("project_id", out _));
        Assert.False(body.TryGetProperty("tag_ids", out _));
    }

    [Fact]
    public async Task TrackerServiceQueuesLinkedAppsLongerThanMinimum()
    {
        _secrets.Value = SavedAccount;
        var toggl = CreateService();
        var probe = new FakeProbe();
        var service = new TrackerService(new DataPaths(_dir.Combine("data")), probe, NullLog.Instance, toggl);
        service.AddApp("clip", "クリスタ", "");
        service.AddApp("code", "VS Code", "");
        service.UpdateApp("clip", a => (a.TogglEnabled, a.TogglProject) = (true, "絵"));
        var settings = service.Settings;
        settings.TogglMinSeconds = 30;
        service.UpdateSettings(settings);

        var now = new DateTime(2026, 10, 29, 9, 0, 0);
        for (var i = 0; i <= 40; i++)
        {
            var t = now.AddSeconds(i);
            probe.Next = () => Snap.At(t, Snap.Window("clip"), Snap.Window("code"));
            service.Tick();
        }
        service.Suspend();
        await toggl.FlushAsync();

        var body = Assert.Single(_api.CreatedEntries);
        Assert.Equal("クリスタ", body.GetProperty("description").GetString());
        Assert.Equal(40, body.GetProperty("duration").GetInt64());
    }

    private sealed class MemorySecrets : ISecretStore
    {
        public string? Value { get; set; }
        public string? Load() => Value;
        public void Save(string? secret) => Value = secret;
    }

    private sealed class FakeToggl : HttpMessageHandler
    {
        public bool Unauthorized { get; set; }
        public HttpStatusCode CreateStatus { get; set; } = HttpStatusCode.Created;
        public int ExtraProjects { get; set; }
        public List<JsonElement> CreatedEntries { get; } = [];
        public string? LastAuthorization { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            LastAuthorization = request.Headers.Authorization?.ToString();
            if (Unauthorized)
            {
                return Json("""{"error":"invalid_session","error_description":"the provided API key is invalid"}""",
                    HttpStatusCode.Unauthorized);
            }

            var query = System.Web.HttpUtility.ParseQueryString(request.RequestUri!.Query);
            return (request.Method.Method, request.RequestUri.AbsolutePath) switch
            {
                ("GET", "/api/organizations/5/workspaces/42/projects") => Page(
                    [(7, "絵"), .. Enumerable.Range(0, ExtraProjects).Select(i => (100L + i, $"p{i}"))], query),
                ("GET", "/api/workspaces/42/tags") => Page([(3, "作業")], query),
                ("POST", "/api/organizations/5/workspaces/42/time-entries") => await Create(request, ct),
                _ => new HttpResponseMessage(HttpStatusCode.NotFound),
            };
        }

        private static HttpResponseMessage Page(List<(long Id, string Name)> all, System.Collections.Specialized.NameValueCollection query)
        {
            var page = int.Parse(query["page"]!);
            var perPage = int.Parse(query["per_page"]!);
            var data = all.Skip((page - 1) * perPage).Take(perPage).Select(i => new { id = i.Id, name = i.Name });
            return Json(JsonSerializer.Serialize(new { data, page, per_page = perPage, total = all.Count }));
        }

        private async Task<HttpResponseMessage> Create(HttpRequestMessage request, CancellationToken ct)
        {
            if (CreateStatus != HttpStatusCode.Created) return Json("""{"error":"error","error_description":"error"}""", CreateStatus);
            var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct)).RootElement.Clone();
            CreatedEntries.Add(body);
            return Json($$"""{"id":{{1000 + CreatedEntries.Count}}}""", HttpStatusCode.Created);
        }

        private static HttpResponseMessage Json(string json, HttpStatusCode status = HttpStatusCode.OK) =>
            new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
    }
}
