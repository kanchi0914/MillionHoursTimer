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

    [Fact]
    public async Task ConnectSavesKeyOnlyWhenAuthenticationSucceeds()
    {
        var service = CreateService();
        _api.Unauthorized = true;
        await Assert.ThrowsAsync<TogglApiException>(() => service.ConnectAsync("wrong"));
        Assert.Null(_secrets.Value);

        _api.Unauthorized = false;
        await service.ConnectAsync("token");
        Assert.Equal("token", _secrets.Value);
        Assert.Equal("me@example.com", service.UserEmail);
        Assert.Equal(["絵"], service.ProjectNames);
        Assert.Equal(["作業"], service.TagNames);
        Assert.Equal("Basic " + Convert.ToBase64String(Encoding.ASCII.GetBytes("token:api_token")), _api.LastAuthorization);
    }

    [Fact]
    public async Task FlushSendsPendingEntriesWithProjectAndTag()
    {
        _secrets.Value = "token";
        var service = CreateService();
        service.Enqueue(Entry("a"));

        await service.FlushAsync();

        var body = Assert.Single(_api.CreatedEntries);
        Assert.Equal(42, body.GetProperty("workspace_id").GetInt64());
        Assert.Equal(7, body.GetProperty("project_id").GetInt64());
        Assert.Equal("作業", body.GetProperty("tags")[0].GetString());
        Assert.Equal(120, body.GetProperty("duration").GetInt64());
        Assert.Equal("クリスタ", body.GetProperty("description").GetString());
        Assert.EndsWith("Z", body.GetProperty("start").GetString());
        Assert.Empty(Queue.LoadPending());
        Assert.Equal(0, service.PendingCount);
    }

    [Fact]
    public async Task ServerErrorsAreRetriedLaterButBadRequestsAreNot()
    {
        _secrets.Value = "token";
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
    public async Task UnknownProjectIsSentWithoutProject()
    {
        _secrets.Value = "token";
        var service = CreateService();
        service.Enqueue(Entry("a", project: "存在しない"));

        await service.FlushAsync();

        Assert.False(Assert.Single(_api.CreatedEntries).TryGetProperty("project_id", out _));
    }

    [Fact]
    public async Task TrackerServiceQueuesLinkedAppsLongerThanMinimum()
    {
        _secrets.Value = "token";
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
        public HttpStatusCode CreateStatus { get; set; } = HttpStatusCode.OK;
        public List<JsonElement> CreatedEntries { get; } = [];
        public string? LastAuthorization { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            LastAuthorization = request.Headers.Authorization?.ToString();
            if (Unauthorized) return new HttpResponseMessage(HttpStatusCode.Forbidden);

            var path = request.RequestUri!.AbsolutePath;
            return (request.Method.Method, path) switch
            {
                ("GET", "/api/v9/me") => Json("""{"email":"me@example.com","default_workspace_id":42}"""),
                ("GET", "/api/v9/workspaces/42/projects") => Json("""[{"id":7,"name":"絵"}]"""),
                ("GET", "/api/v9/workspaces/42/tags") => Json("""[{"id":3,"name":"作業"}]"""),
                ("POST", "/api/v9/workspaces/42/time_entries") => await Create(request, ct),
                _ => new HttpResponseMessage(HttpStatusCode.NotFound),
            };
        }

        private async Task<HttpResponseMessage> Create(HttpRequestMessage request, CancellationToken ct)
        {
            if (CreateStatus != HttpStatusCode.OK) return new HttpResponseMessage(CreateStatus) { Content = new StringContent("error") };
            var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct)).RootElement.Clone();
            CreatedEntries.Add(body);
            return Json($$"""{"id":{{1000 + CreatedEntries.Count}}}""");
        }

        private static HttpResponseMessage Json(string json) =>
            new(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
    }
}
