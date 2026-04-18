using System.Diagnostics;
using System.Text;

namespace PCL.Frontend.Avalonia.Workflows;

internal enum FrontendDesktopPlatformKind
{
    Windows,
    Linux,
    MacOS,
    Other
}

internal sealed class FrontendPlatformAdapter
{
    public FrontendDesktopPlatformKind GetDesktopPlatformKind()
    {
        if (OperatingSystem.IsWindows())
        {
            return FrontendDesktopPlatformKind.Windows;
        }

        if (OperatingSystem.IsLinux())
        {
            return FrontendDesktopPlatformKind.Linux;
        }

        if (OperatingSystem.IsMacOS())
        {
            return FrontendDesktopPlatformKind.MacOS;
        }

        return FrontendDesktopPlatformKind.Other;
    }

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

    public string? TryGetLauncherEntryDirectory()
    {
        if (OperatingSystem.IsWindows())
        {
            var startMenuDirectory = Environment.GetFolderPath(Environment.SpecialFolder.StartMenu);
            return string.IsNullOrWhiteSpace(startMenuDirectory)
                ? null
                : Path.Combine(startMenuDirectory, "Programs");
        }

        if (OperatingSystem.IsLinux())
        {
            return FrontendLinuxDesktopEntryService.ResolveApplicationsDirectory();
        }

        if (OperatingSystem.IsMacOS())
        {
            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return string.IsNullOrWhiteSpace(userProfile)
                ? null
                : Path.Combine(userProfile, "Applications");
        }

        return null;
    }

    public bool TryOpenExternalTarget(string target, out string? error)
    {
        error = null;

        if (string.IsNullOrWhiteSpace(target))
        {
            error = "Missing target to open.";
            return false;
        }

        try
        {
            var startInfo = BuildOpenTargetStartInfo(target);
            using var process = Process.Start(startInfo);
            if (!IsSuccessfulStart(startInfo, process))
            {
                error = "The system did not return a usable process for opening the target.";
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
            error = "Missing target to reveal.";
            return false;
        }

        try
        {
            var startInfo = BuildRevealTargetStartInfo(target);
            using var process = Process.Start(startInfo);
            if (!IsSuccessfulStart(startInfo, process))
            {
                error = "The system did not return a usable process for revealing the target.";
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
            error = "No executable update script was found.";
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
                error = "The system did not return a usable update process.";
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
            link.Description = $"{displayName} shortcut";
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
            shortcutContent = FrontendLinuxDesktopEntryService.BuildDesktopEntry(
                displayName,
                executablePath,
                FrontendLinuxDesktopEntryService.ResolveIconPath(executablePath));
        }

        File.WriteAllText(shortcutPath, shortcutContent, new UTF8Encoding(false));
        EnsureFileExecutable(shortcutPath);
        return new FrontendShortcutMaterializationResult(shortcutPath);
    }

    public FrontendShortcutMaterializationResult CreateDirectLaunchShortcut(
        string targetDirectory,
        string executablePath,
        string instanceName,
        string displayName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(instanceName);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);

        Directory.CreateDirectory(targetDirectory);

        var executableDirectory = Path.GetDirectoryName(executablePath) ?? Path.GetPathRoot(executablePath) ?? targetDirectory;
        var arguments = new[] { "launch-instance", "--instance", instanceName };

        if (OperatingSystem.IsWindows())
        {
            var shortcutPath = Path.Combine(targetDirectory, $"{SanitizeFileName(displayName)}.lnk");
            var shellType = Type.GetTypeFromProgID("WScript.Shell", throwOnError: true)!;
            dynamic shell = Activator.CreateInstance(shellType)!;
            var link = shell.CreateShortcut(shortcutPath)!;
            link.TargetPath = executablePath;
            link.Arguments = JoinWindowsArguments(arguments);
            link.WorkingDirectory = executableDirectory;
            link.Description = $"{displayName} shortcut";
            link.IconLocation = executablePath;
            link.Save();
            return new FrontendShortcutMaterializationResult(shortcutPath);
        }

        if (OperatingSystem.IsMacOS())
        {
            var appBundlePath = Path.Combine(targetDirectory, $"{SanitizeFileName(displayName)}.app");
            WriteMacApplicationBundle(appBundlePath, executablePath, arguments, displayName);
            return new FrontendShortcutMaterializationResult(appBundlePath);
        }

        var shortcutScriptPath = Path.Combine(
            targetDirectory,
            $"{BuildLauncherScriptFileName(displayName)}.sh");
        WritePosixLauncherScript(shortcutScriptPath, executablePath, arguments);

        var shortcutPathLinux = Path.Combine(targetDirectory, $"{SanitizeFileName(displayName)}.desktop");
        var shortcutContentLinux = FrontendLinuxDesktopEntryService.BuildDesktopEntry(
            displayName,
            [shortcutScriptPath],
            FrontendLinuxDesktopEntryService.ResolveIconPath(executablePath),
            executableDirectory);
        File.WriteAllText(shortcutPathLinux, shortcutContentLinux, new UTF8Encoding(false));
        EnsureFileExecutable(shortcutPathLinux);
        return new FrontendShortcutMaterializationResult(shortcutPathLinux);
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

        var executableName = GetJavaExecutableFileName();
        var directPath = Path.Combine(runtimeDirectory, "bin", executableName);
        if (File.Exists(directPath) || !OperatingSystem.IsMacOS())
        {
            return directPath;
        }

        var bundleCandidates = new[]
        {
            Path.Combine(runtimeDirectory, "Contents", "Home", "bin", executableName),
            Path.Combine(runtimeDirectory, "jre.bundle", "Contents", "Home", "bin", executableName)
        };
        foreach (var candidate in bundleCandidates)
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        try
        {
            foreach (var bundleDirectory in Directory.EnumerateDirectories(runtimeDirectory))
            {
                var extension = Path.GetExtension(bundleDirectory);
                if (!extension.Equals(".bundle", StringComparison.OrdinalIgnoreCase) &&
                    !extension.Equals(".jdk", StringComparison.OrdinalIgnoreCase) &&
                    !extension.Equals(".jre", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var candidate = Path.Combine(bundleDirectory, "Contents", "Home", "bin", executableName);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
        }
        catch
        {
            // Fall back to the canonical root/bin path if the directory cannot be enumerated.
        }

        return directPath;
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

    private void WriteMacApplicationBundle(
        string appBundlePath,
        string executablePath,
        IReadOnlyList<string> arguments,
        string displayName)
    {
        var contentsDirectory = Path.Combine(appBundlePath, "Contents");
        var macOsDirectory = Path.Combine(contentsDirectory, "MacOS");
        Directory.CreateDirectory(macOsDirectory);

        var executableName = SanitizeFileName(displayName);
        var launcherScriptPath = Path.Combine(macOsDirectory, executableName);
        WritePosixLauncherScript(launcherScriptPath, executablePath, arguments);

        var infoPlistPath = Path.Combine(contentsDirectory, "Info.plist");
        var infoPlist = $$"""
            <?xml version="1.0" encoding="UTF-8"?>
            <!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
            <plist version="1.0">
            <dict>
                <key>CFBundleDevelopmentRegion</key>
                <string>en</string>
                <key>CFBundleDisplayName</key>
                <string>{{EscapeXmlValue(displayName)}}</string>
                <key>CFBundleExecutable</key>
                <string>{{EscapeXmlValue(executableName)}}</string>
                <key>CFBundleIdentifier</key>
                <string>{{EscapeXmlValue(BuildMacBundleIdentifier(displayName))}}</string>
                <key>CFBundleName</key>
                <string>{{EscapeXmlValue(displayName)}}</string>
                <key>CFBundlePackageType</key>
                <string>APPL</string>
                <key>CFBundleVersion</key>
                <string>1</string>
                <key>LSApplicationCategoryType</key>
                <string>public.app-category.games</string>
                <key>LSUIElement</key>
                <true/>
            </dict>
            </plist>
            """;
        File.WriteAllText(infoPlistPath, infoPlist, new UTF8Encoding(false));
    }

    private void WritePosixLauncherScript(
        string scriptPath,
        string executablePath,
        IReadOnlyList<string> arguments)
    {
        var launcherScript = string.Join(
            Environment.NewLine,
            [
                "#!/bin/sh",
                $"exec {EscapePosixArgument(executablePath)} {string.Join(" ", arguments.Select(EscapePosixArgument))}"
            ]);
        File.WriteAllText(scriptPath, launcherScript, new UTF8Encoding(false));
        EnsureFileExecutable(scriptPath);
    }

    private static string JoinWindowsArguments(IEnumerable<string> arguments)
    {
        return string.Join(" ", arguments.Select(EscapeWindowsArgument));
    }

    private static string EscapeWindowsArgument(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "\"\"";
        }

        if (value.IndexOfAny([' ', '\t', '"']) < 0)
        {
            return value;
        }

        return "\"" + value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";
    }

    private static string EscapePosixArgument(string value)
    {
        return "'" + value.Replace("'", "'\"'\"'", StringComparison.Ordinal) + "'";
    }

    private static string EscapeXmlValue(string value)
    {
        return value
            .Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal);
    }

    private static string BuildMacBundleIdentifier(string displayName)
    {
        var slug = new string(displayName
            .ToLowerInvariant()
            .Select(character => char.IsLetterOrDigit(character) ? character : '-')
            .ToArray())
            .Trim('-');
        if (string.IsNullOrWhiteSpace(slug))
        {
            slug = "instance";
        }

        while (slug.Contains("--", StringComparison.Ordinal))
        {
            slug = slug.Replace("--", "-", StringComparison.Ordinal);
        }

        return $"org.pcl.me.frontend.instance.{slug}";
    }

    private static string BuildLauncherScriptFileName(string value)
    {
        var sanitized = SanitizeFileName(value)
            .Replace(" ", "-", StringComparison.Ordinal)
            .Replace("--", "-", StringComparison.Ordinal);
        return string.IsNullOrWhiteSpace(sanitized) ? "pcl-me-launch" : sanitized;
    }

    private static string SanitizeFileName(string value)
    {
        var invalidCharacters = Path.GetInvalidFileNameChars();
        var sanitized = new string(value.Select(character => invalidCharacters.Contains(character) ? '-' : character).ToArray()).Trim();
        return string.IsNullOrWhiteSpace(sanitized) ? "PCL-ME" : sanitized;
    }
}

internal sealed record FrontendShortcutMaterializationResult(string ShortcutPath);
