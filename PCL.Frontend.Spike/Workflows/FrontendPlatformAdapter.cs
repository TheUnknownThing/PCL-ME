using System.Diagnostics;
using System.Text;

namespace PCL.Frontend.Spike.Workflows;

internal sealed class FrontendPlatformAdapter
{
    public string GetLauncherAppDataDirectory()
    {
        if (OperatingSystem.IsWindows())
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PCL");
        }

        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".config",
            "PCL");
    }

    public string? TryGetDesktopDirectory()
    {
        var desktopDirectory = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        if (!string.IsNullOrWhiteSpace(desktopDirectory))
        {
            return desktopDirectory;
        }

        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return string.IsNullOrWhiteSpace(userProfile)
            ? null
            : Path.Combine(userProfile, "Desktop");
    }

    public bool TryOpenExternalTarget(string target, out string? error)
    {
        error = null;

        if (string.IsNullOrWhiteSpace(target))
        {
            error = "缺少可打开的目标。";
            return false;
        }

        try
        {
            using var process = Process.Start(BuildOpenTargetStartInfo(target));
            if (process is null)
            {
                error = "系统未返回可用的打开进程。";
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    public FrontendShortcutMaterializationResult CreateLauncherShortcut(
        string desktopDirectory,
        string executablePath,
        string displayName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(desktopDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);

        Directory.CreateDirectory(desktopDirectory);

        string shortcutPath;
        string shortcutContent;

        if (OperatingSystem.IsWindows())
        {
            shortcutPath = Path.Combine(desktopDirectory, $"{displayName}.cmd");
            shortcutContent = $"""
                @echo off
                start "" "{executablePath}"
                """;
        }
        else if (OperatingSystem.IsMacOS())
        {
            shortcutPath = Path.Combine(desktopDirectory, $"{displayName}.command");
            shortcutContent = $"""
                #!/bin/sh
                "{executablePath}" "$@"
                """;
        }
        else
        {
            shortcutPath = Path.Combine(desktopDirectory, $"{displayName}.desktop");
            shortcutContent = $"""
                [Desktop Entry]
                Type=Application
                Name={displayName}
                Exec="{executablePath}"
                Terminal=false
                """;
        }

        File.WriteAllText(shortcutPath, shortcutContent, new UTF8Encoding(false));
        EnsureFileExecutable(shortcutPath);
        return new FrontendShortcutMaterializationResult(shortcutPath);
    }

    public string GetCommandScriptExtension()
    {
        return OperatingSystem.IsWindows()
            ? ".cmd"
            : OperatingSystem.IsMacOS()
                ? ".command"
                : ".sh";
    }

    public string GetJavaExecutableFileName()
    {
        return OperatingSystem.IsWindows() ? "java.exe" : "java";
    }

    public string GetJavaExecutablePath(string runtimeDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runtimeDirectory);
        return Path.Combine(runtimeDirectory, "bin", GetJavaExecutableFileName());
    }

    public IReadOnlyList<string> GetDefaultJavaDetectionCandidates()
    {
        return
        [
            Path.Combine(Environment.GetEnvironmentVariable("JAVA_HOME") ?? string.Empty, "bin", GetJavaExecutableFileName()),
            OperatingSystem.IsWindows() ? @"C:\Program Files\Java\bin\java.exe" : "/usr/bin/java"
        ];
    }

    public void EnsureFileExecutable(string path)
    {
        if (OperatingSystem.IsWindows() || string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return;
        }

        try
        {
            File.SetUnixFileMode(
                path,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
                UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
        }
        catch
        {
            // Best effort on Unix-like systems.
        }
    }

    private ProcessStartInfo BuildOpenTargetStartInfo(string target)
    {
        if (OperatingSystem.IsWindows())
        {
            return new ProcessStartInfo
            {
                FileName = target,
                UseShellExecute = true
            };
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = OperatingSystem.IsMacOS() ? "open" : "xdg-open",
            UseShellExecute = false
        };
        startInfo.ArgumentList.Add(target);
        return startInfo;
    }
}

internal sealed record FrontendShortcutMaterializationResult(string ShortcutPath);
