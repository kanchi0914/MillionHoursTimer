using System.Text;
using MillionHoursTimer.Core.Legacy;
using MillionHoursTimer.Core.Model;

namespace MillionHoursTimer.Core.Tests;

public class LegacyImporterTests : IDisposable
{
    private readonly TempDir _dir = new();

    public void Dispose() => _dir.Dispose();

    /// <summary>旧バージョンと同じ形式 (BOM 付き UTF-8、カンマで単純に区切る) で data フォルダを作る</summary>
    private string CreateLegacyData()
    {
        var data = _dir.Combine("MHTimer", "data");
        Directory.CreateDirectory(Path.Combine(data, "fileData"));
        Directory.CreateDirectory(Path.Combine(data, "icons"));
        var utf8 = new UTF8Encoding(true);
        File.WriteAllText(Path.Combine(data, "appData.csv"),
            "アプリケーション名,今日の起動時間,累積起動時間,最終起動日時,toggle連携フラグ,連携プロジェクト名,連携タグ名, ファイル拡張子\r\n" +
            "CLIPStudioPaint,01:00:00,123:04:05,2022/05/01 09:15,True,イラスト,作業,.clip/.png\r\n" +
            "Photoshop,-,-,-,False,,,.gif/.jpg/.jpeg/.png/.bmp/.ico/.tif/.tiff/.tga/.psd/.psb/.sai\r\n" +
            "devenv,00:00:00,10:00:00,,False,,,\r\n", utf8);
        File.WriteAllText(Path.Combine(data, "fileData", "CLIPStudioPaint_files.csv"),
            "ファイル名,累積作業時間\r\n表紙.clip,10:00:00\r\nラフ, その2.png,00:30:00\r\nゼロ.png,-\r\n", utf8);
        File.WriteAllBytes(Path.Combine(data, "icons", "CLIPStudioPaint.png"), [1, 2, 3]);
        return data;
    }

    [Fact]
    public void ReadsAppsFilesAndIcons()
    {
        var data = CreateLegacyData();

        var legacy = LegacyImporter.Read(data);

        Assert.Equal(3, legacy.Apps.Count);
        var csp = legacy.Apps[0];
        Assert.Equal("CLIPStudioPaint", csp.Process);
        Assert.Equal(123 * 3600 + 4 * 60 + 5, csp.TotalSeconds);
        Assert.True(csp.TogglEnabled);
        Assert.Equal(("イラスト", "作業"), (csp.TogglProject, csp.TogglTag));
        Assert.Equal([".clip", ".png"], csp.Extensions);
        Assert.NotNull(csp.IconPath);
        Assert.Equal([new LegacyFile("表紙.clip", 36000), new LegacyFile("ラフ, その2.png", 1800)], csp.Files);

        Assert.Equal(0, legacy.Apps[1].TotalSeconds);
        Assert.Equal(12, legacy.Apps[1].Extensions.Count);
        Assert.Empty(legacy.Apps[2].Extensions);
        Assert.Empty(legacy.Warnings);
    }

    [Fact]
    public void ResolvesDataDirFromExeFolder()
    {
        var data = CreateLegacyData();
        Assert.Equal(data, LegacyImporter.ResolveDataDir(Path.GetDirectoryName(data)!));
        Assert.Equal(data, LegacyImporter.ResolveDataDir(data));
        Assert.Null(LegacyImporter.ResolveDataDir(_dir.Path));
    }

    [Fact]
    public void ReadsUserConfigAndMapsSettings()
    {
        var dir = _dir.Combine("LocalAppData", "MHTimer", "MHTimer.exe_Url_abc123", "1.0.0.0");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "user.config");
        File.WriteAllText(path, """
            <?xml version="1.0" encoding="utf-8"?>
            <configuration>
              <userSettings>
                <MHTimer.Properties.Settings>
                  <setting name="IsCountingNotMinimized" serializeAs="String"><value>True</value></setting>
                  <setting name="IsCountingOnlyActive" serializeAs="String"><value>True</value></setting>
                  <setting name="APIKey" serializeAs="String"><value>secret-token</value></setting>
                  <setting name="NoInputTime" serializeAs="String"><value>900</value></setting>
                  <setting name="CountingSecondsInterval" serializeAs="String"><value>5</value></setting>
                  <setting name="IsDividingBySpace" serializeAs="String"><value>False</value></setting>
                  <setting name="isIgnoringChindsWindows" serializeAs="String"><value>True</value></setting>
                  <setting name="IsAutoLaunch" serializeAs="String"><value>True</value></setting>
                </MHTimer.Properties.Settings>
              </userSettings>
            </configuration>
            """);

        Assert.Equal(path, LegacyImporter.FindUserConfig(_dir.Combine("LocalAppData")));
        var legacy = LegacyImporter.ReadUserConfig(path)!;
        var settings = new AppSettings();
        legacy.ApplyTo(settings);

        Assert.Equal("secret-token", legacy.ApiKey);
        Assert.Equal(CountMode.ActiveOnly, settings.CountMode);
        Assert.Equal(900, settings.IdleTimeoutSeconds);
        Assert.Equal(5, settings.CountIntervalSeconds);
        Assert.False(settings.SplitFileNameBySpace);
        Assert.True(settings.IgnoreWindowStateForFiles);
        Assert.True(settings.AutoStart);
        Assert.True(settings.UseHyphenFileName);   // 値がないものは既定値のまま
    }
}
