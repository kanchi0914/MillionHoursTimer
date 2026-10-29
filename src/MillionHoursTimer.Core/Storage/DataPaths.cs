namespace MillionHoursTimer.Core.Storage;

/// <summary>
/// データの保存場所。既定は %APPDATA%\MillionHoursTimer。
/// (インストーラーがアンインストール時に消す %LOCALAPPDATA%\MillionHoursTimer とは分けている)
/// </summary>
public sealed class DataPaths
{
    public const string FolderName = "MillionHoursTimer";

    public DataPaths(string root)
    {
        Root = Path.GetFullPath(root);
    }

    public string Root { get; }
    public string SettingsFile => Path.Combine(Root, "settings.json");
    public string AppsFile => Path.Combine(Root, "apps.csv");
    public string RecordsDir => Path.Combine(Root, "records");
    public string AppRecordsDir => Path.Combine(RecordsDir, "apps");
    public string FileRecordsDir => Path.Combine(RecordsDir, "files");
    public string TogglLogFile => Path.Combine(Root, "toggl_log.csv");
    public string TogglKeyFile => Path.Combine(Root, "toggl.key");
    public string CheckpointFile => Path.Combine(Root, "current.json");
    public string IconsDir => Path.Combine(Root, "icons");
    public string LogsDir => Path.Combine(Root, "logs");

    public string IconFile(string process) => Path.Combine(IconsDir, process + ".png");

    public static DataPaths Default() =>
        new(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), FolderName));

    public void EnsureCreated()
    {
        Directory.CreateDirectory(AppRecordsDir);
        Directory.CreateDirectory(FileRecordsDir);
        Directory.CreateDirectory(IconsDir);
        Directory.CreateDirectory(LogsDir);
    }
}
