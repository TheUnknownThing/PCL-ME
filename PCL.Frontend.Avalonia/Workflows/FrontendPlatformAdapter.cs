using System.Diagnostics;
using System.Text;

namespace PCL.Frontend.Avalonia.Workflows;

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
            var startInfo = BuildOpenTargetStartInfo(target);
            using var process = Process.Start(startInfo);
            if (!IsSuccessfulStart(startInfo, process))
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

    public bool TryRevealExternalTarget(string target, out string? error)
    {
        error = null;

        if (string.IsNullOrWhiteSpace(target))
        {
            error = "缺少可定位的目标。";
            return false;
        }

        try
        {
            var startInfo = BuildRevealTargetStartInfo(target);
            using var process = Process.Start(startInfo);
            if (!IsSuccessfulStart(startInfo, process))
            {
                error = "系统未返回可用的定位进程。";
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

    public bool TryStartDetachedScript(string scriptPath, out string? error)
    {
        error = null;

        if (string.IsNullOrWhiteSpace(scriptPath) || !File.Exists(scriptPath))
        {
            error = "未找到可执行的更新脚本。";
            return false;
        }

        try
        {
            ProcessStartInfo startInfo;
            if (OperatingSystem.IsWindows())
            {
                startInfo = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c start \"\" \"{scriptPath}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
            }
            else
            {
                startInfo = new ProcessStartInfo
                {
                    FileName = "/bin/sh",
                    UseShellExecute = false
                };
                startInfo.ArgumentList.Add(scriptPath);
            }

            using var process = Process.Start(startInfo);
            if (!IsSuccessfulStart(startInfo, process))
            {
                error = "系统未返回可用的更新进程。";
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
            shortcutPath = Path.Combine(desktopDirectory, $"{displayName}.lnk");
            var shellType = Type.GetTypeFromProgID("WScript.Shell", throwOnError: true)!;
            dynamic shell = Activator.CreateInstance(shellType)!;
            var link = shell.CreateShortcut(shortcutPath)!;
            link.TargetPath = executablePath;
            link.WorkingDirectory = Path.GetDirectoryName(executablePath) ?? Path.GetPathRoot(executablePath);
            link.Description = $"{displayName} 快捷方式";
            link.Save();
            return new FrontendShortcutMaterializationResult(shortcutPath);
        }
        
        if (OperatingSystem.IsMacOS())
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

    private ProcessStartInfo BuildRevealTargetStartInfo(string target)
    {
        var fullTargetPath = Path.GetFullPath(target);
        var isDirectory = Directory.Exists(fullTargetPath);

        if (OperatingSystem.IsWindows())
        {
            return isDirectory
                ? new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"\"{fullTargetPath}\"",
                    UseShellExecute = false
                }
                : new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"/select,\"{fullTargetPath}\"",
                    UseShellExecute = false
                };
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = OperatingSystem.IsMacOS() ? "open" : "xdg-open",
            UseShellExecute = false
        };

        if (OperatingSystem.IsMacOS() && !isDirectory)
        {
            startInfo.ArgumentList.Add("-R");
            startInfo.ArgumentList.Add(fullTargetPath);
            return startInfo;
        }

        startInfo.ArgumentList.Add(isDirectory ? fullTargetPath : Path.GetDirectoryName(fullTargetPath) ?? fullTargetPath);
        return startInfo;
    }

    internal static bool IsSuccessfulStart(ProcessStartInfo startInfo, Process? process)
    {
        ArgumentNullException.ThrowIfNull(startInfo);
        return process is not null || startInfo.UseShellExecute;
    }
}

internal sealed record FrontendShortcutMaterializationResult(string ShortcutPath);
