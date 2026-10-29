namespace MillionHoursTimer.Core;

public interface ILog
{
    void Info(string message);
    void Warn(string message);
    void Error(string message, Exception? ex = null);
}

public sealed class NullLog : ILog
{
    public static readonly NullLog Instance = new();
    public void Info(string message) { }
    public void Warn(string message) { }
    public void Error(string message, Exception? ex = null) { }
}

/// <summary>logs\yyyy-MM-dd.log に書くログ。古いものは自動で消す。</summary>
public sealed class FileLog : ILog
{
    private const int KeepDays = 30;
    private readonly string _dir;
    private readonly object _gate = new();

    public FileLog(string dir)
    {
        _dir = dir;
        try
        {
            Directory.CreateDirectory(dir);
            foreach (var f in new DirectoryInfo(dir).GetFiles("*.log").OrderByDescending(f => f.Name).Skip(KeepDays))
            {
                f.Delete();
            }
        }
        catch (IOException)
        {
        }
    }

    public void Info(string message) => Write("INFO", message);
    public void Warn(string message) => Write("WARN", message);
    public void Error(string message, Exception? ex = null) => Write("ERROR", ex == null ? message : $"{message}\n{ex}");

    private void Write(string level, string message)
    {
        lock (_gate)
        {
            try
            {
                var path = Path.Combine(_dir, $"{DateTime.Now:yyyy-MM-dd}.log");
                File.AppendAllText(path, $"{DateTime.Now:HH:mm:ss} [{level}] {message}\r\n");
            }
            catch (IOException)
            {
            }
        }
    }
}

/// <summary>API キーなどの秘密情報の保存先 (Windows では DPAPI で暗号化する)</summary>
public interface ISecretStore
{
    string? Load();
    void Save(string? secret);
}
