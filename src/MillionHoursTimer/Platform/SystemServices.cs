using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32;
using MillionHoursTimer.Core;

namespace MillionHoursTimer.Platform;

/// <summary>Windows 起動時の自動起動 (HKCU\...\Run)</summary>
internal static class AutoStart
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

    // 旧バージョンの値の名前は "MHTimer"。旧バージョンをアンインストールすると "MHTimer" が消されるので、別の名前にしている
    private const string ValueName = "MillionHoursTimer";

    public const string MinimizedArg = "--minimized";

    private static string Command => $"\"{Environment.ProcessPath}\" {MinimizedArg}";

    public static bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey);
        return key?.GetValue(ValueName) is string value && value == Command;
    }

    public static void Set(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey, writable: true);
        if (enabled) key.SetValue(ValueName, Command);
        else key.DeleteValue(ValueName, throwOnMissingValue: false);
    }
}

/// <summary>Toggl の API キーを DPAPI で暗号化して保存する (同じ Windows ユーザーでしか復号できない)</summary>
internal sealed class DpapiSecretStore(string path, ILog log) : ISecretStore
{
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("MillionHoursTimer");

    public string? Load()
    {
        try
        {
            if (!File.Exists(path)) return null;
            var data = Convert.FromBase64String(File.ReadAllText(path).Trim());
            return Encoding.UTF8.GetString(ProtectedData.Unprotect(data, Entropy, DataProtectionScope.CurrentUser));
        }
        catch (Exception ex) when (ex is CryptographicException or FormatException or IOException)
        {
            // 別の PC にデータフォルダをコピーした場合など。キーを入れ直してもらう
            log.Warn($"Toggl の API キーを読めませんでした。設定画面から入力し直してください ({ex.Message})");
            return null;
        }
    }

    public void Save(string? secret)
    {
        if (string.IsNullOrEmpty(secret))
        {
            File.Delete(path);
            return;
        }
        var data = ProtectedData.Protect(Encoding.UTF8.GetBytes(secret), Entropy, DataProtectionScope.CurrentUser);
        File.WriteAllText(path, Convert.ToBase64String(data));
    }
}

/// <summary>ショートカット (.lnk) のリンク先を調べる</summary>
internal static class Shortcut
{
    public static string? ResolveTarget(string lnkPath)
    {
        var type = Type.GetTypeFromProgID("WScript.Shell");
        if (type == null) return null;
        dynamic shell = Activator.CreateInstance(type)!;
        try
        {
            dynamic shortcut = shell.CreateShortcut(lnkPath);
            string target = shortcut.TargetPath;
            return string.IsNullOrEmpty(target) ? null : target;
        }
        finally
        {
            System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shell);
        }
    }
}

internal static class Shell
{
    public static void Open(string pathOrUrl) =>
        Process.Start(new ProcessStartInfo(pathOrUrl) { UseShellExecute = true });
}
