using System.Diagnostics;
using System.IO;
using Microsoft.Win32;
using MillionHoursTimer.Core.Legacy;

namespace MillionHoursTimer.Platform;

/// <summary>旧バージョン (MHTimer v1.x) のインストール先やデータを探す</summary>
internal static class LegacyLocator
{
    private const string OldName = "MHTimer";
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

    private static readonly string[] UninstallKeys =
    [
        @"Software\Microsoft\Windows\CurrentVersion\Uninstall",
        @"Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall",
    ];

    /// <summary>旧バージョンの data フォルダの候補。最近更新されたものから順に返す。</summary>
    public static List<string> FindDataDirs()
    {
        var exeDirs = new List<string?>();

        // バイナリ版でも、自動起動を設定していればここから場所が分かる
        if (OldRunCommand() is { } command) exeDirs.Add(Path.GetDirectoryName(ExtractExePath(command)));
        exeDirs.AddRange(RunningOldInstances().Select(SafeExeDir));
        exeDirs.AddRange(InstalledEntries().Select(e => e.InstallLocation));

        foreach (var programFiles in new[] { Environment.SpecialFolder.ProgramFilesX86, Environment.SpecialFolder.ProgramFiles })
        {
            var dir = Path.Combine(Environment.GetFolderPath(programFiles), OldName);
            exeDirs.Add(dir);
            // UAC の仮想化でこちらに書かれていることがある
            var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            exeDirs.Add(Path.Combine(local, "VirtualStore", Path.GetFileName(Environment.GetFolderPath(programFiles)), OldName));
        }

        return exeDirs
            .Where(d => !string.IsNullOrWhiteSpace(d))
            .Select(d => LegacyImporter.ResolveDataDir(d!))
            .OfType<string>()
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(d => File.GetLastWriteTime(Path.Combine(d, LegacyImporter.AppDataFileName)))
            .ToList();
    }

    public static string? FindUserConfig() =>
        LegacyImporter.FindUserConfig(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));

    /// <summary>起動中の旧バージョン</summary>
    public static List<Process> RunningOldInstances() =>
        Process.GetProcessesByName(OldName).Where(p => p.Id != Environment.ProcessId).ToList();

    /// <summary>旧バージョンの自動起動の設定 (なければ null)</summary>
    public static string? OldRunCommand()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey);
        return key?.GetValue(OldName) as string;
    }

    public static void RemoveOldRunCommand()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
        key?.DeleteValue(OldName, throwOnMissingValue: false);
    }

    /// <summary>インストーラー版の旧バージョンがインストールされているか</summary>
    public static bool IsOldInstallerPresent() => InstalledEntries().Any();

    private static IEnumerable<(string DisplayName, string? InstallLocation)> InstalledEntries()
    {
        foreach (var hive in new[] { Registry.CurrentUser, Registry.LocalMachine })
        {
            foreach (var path in UninstallKeys)
            {
                using var root = hive.OpenSubKey(path);
                if (root == null) continue;
                foreach (var name in root.GetSubKeyNames())
                {
                    using var entry = root.OpenSubKey(name);
                    if (entry?.GetValue("DisplayName") is not string displayName || displayName != OldName) continue;
                    yield return (displayName, entry.GetValue("InstallLocation") as string);
                }
            }
        }
    }

    private static string ExtractExePath(string command)
    {
        command = command.Trim();
        if (command.StartsWith('"'))
        {
            var end = command.IndexOf('"', 1);
            return end > 0 ? command[1..end] : command.Trim('"');
        }
        var exe = command.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
        return exe > 0 ? command[..(exe + 4)] : command;
    }

    private static string? SafeExeDir(Process p)
    {
        try
        {
            return Path.GetDirectoryName(p.MainModule?.FileName);
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            return null;
        }
    }
}
