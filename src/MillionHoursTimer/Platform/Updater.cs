using MillionHoursTimer.Core;
using Velopack;
using Velopack.Sources;

namespace MillionHoursTimer.Platform;

/// <summary>GitHub Releases からの自動更新 (インストーラー版・ポータブル版のときだけ動く)</summary>
internal sealed class Updater(ILog log)
{
    public const string RepositoryUrl = "https://github.com/kanchi0914/MillionHoursTimer";

    private readonly UpdateManager _manager = new(new GithubSource(RepositoryUrl, null, false));
    private UpdateInfo? _available;

    /// <summary>開発中に dotnet run したときなどは false</summary>
    public bool IsAvailable => _manager.IsInstalled;

    public string? AvailableVersion => _available?.TargetFullRelease.Version.ToString();

    public async Task<string?> CheckAsync()
    {
        if (!IsAvailable) return null;
        try
        {
            _available = await _manager.CheckForUpdatesAsync();
            return AvailableVersion;
        }
        catch (Exception ex)
        {
            log.Warn($"更新の確認に失敗しました: {ex.Message}");
            return null;
        }
    }

    /// <summary>更新をダウンロードする。終わったら <see cref="ApplyAndRestart"/> で再起動して適用する。</summary>
    public async Task DownloadAsync(Action<int>? progress = null)
    {
        if (_available == null) return;
        await _manager.DownloadUpdatesAsync(_available, progress);
    }

    public void ApplyAndRestart()
    {
        if (_available != null) _manager.ApplyUpdatesAndRestart(_available);
    }
}
