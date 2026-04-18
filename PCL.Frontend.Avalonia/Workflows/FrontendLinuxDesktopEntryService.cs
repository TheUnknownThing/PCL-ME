using System.Text;

namespace PCL.Frontend.Avalonia.Workflows;

internal static class FrontendLinuxDesktopEntryService
{
    public static void EnsureRegistered()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        var executablePath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            return;
        }

        var applicationsDirectory = ResolveApplicationsDirectory();
        if (string.IsNullOrWhiteSpace(applicationsDirectory))
        {
            return;
        }

        Directory.CreateDirectory(applicationsDirectory);

        var desktopEntryPath = Path.Combine(
            applicationsDirectory,
            FrontendApplicationIdentity.LinuxDesktopFileId + ".desktop");
        var desktopEntryContent = BuildDesktopEntry(
            FrontendApplicationIdentity.DisplayName,
            [executablePath],
            ResolveIconPath(executablePath),
            Path.GetDirectoryName(executablePath));

        WriteIfChanged(desktopEntryPath, desktopEntryContent);
    }

    public static string BuildDesktopEntry(string displayName, string executablePath, string? iconPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);

        return BuildDesktopEntry(
            displayName,
            [executablePath],
            iconPath,
            Path.GetDirectoryName(executablePath));
    }

    public static string BuildDesktopEntry(
        string displayName,
        IReadOnlyList<string> execArguments,
        string? iconPath,
        string? workingDirectory = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentNullException.ThrowIfNull(execArguments);
        if (execArguments.Count == 0 || execArguments.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException("Desktop entry arguments cannot be empty.", nameof(execArguments));
        }

        var builder = new StringBuilder();
        builder.AppendLine("[Desktop Entry]");
        builder.AppendLine("Type=Application");
        builder.AppendLine($"Name={EscapeDesktopValue(displayName)}");
        builder.AppendLine("Comment=PCL-ME frontend");
        builder.AppendLine($"Exec={string.Join(" ", execArguments.Select(EscapeExecArgument))}");

        if (!string.IsNullOrWhiteSpace(workingDirectory))
        {
            builder.AppendLine($"Path={EscapeDesktopValue(workingDirectory)}");
        }

        if (!string.IsNullOrWhiteSpace(iconPath))
        {
            builder.AppendLine($"Icon={EscapeDesktopValue(iconPath)}");
        }

        builder.AppendLine($"StartupWMClass={FrontendApplicationIdentity.LinuxWindowClass}");
        builder.AppendLine("Terminal=false");
        builder.AppendLine("Categories=Game;");
        builder.AppendLine("StartupNotify=true");
        return builder.ToString();
    }

    public static string? ResolveApplicationsDirectory()
    {
        var dataHome = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
        if (!string.IsNullOrWhiteSpace(dataHome))
        {
            return Path.Combine(dataHome, "applications");
        }

        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return string.IsNullOrWhiteSpace(userProfile)
            ? null
            : Path.Combine(userProfile, ".local", "share", "applications");
    }

    public static string? ResolveIconPath(string executablePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);

        var executableDirectory = Path.GetDirectoryName(executablePath);
        if (string.IsNullOrWhiteSpace(executableDirectory))
        {
            return null;
        }

        var iconPath = Path.Combine(executableDirectory, FrontendApplicationIdentity.LinuxIconRelativePath);
        return File.Exists(iconPath) ? iconPath : null;
    }

    private static void WriteIfChanged(string path, string content)
    {
        if (File.Exists(path))
        {
            var existing = File.ReadAllText(path);
            if (string.Equals(existing, content, StringComparison.Ordinal))
            {
                return;
            }
        }

        File.WriteAllText(path, content, new UTF8Encoding(false));
    }

    private static string EscapeDesktopValue(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal);
    }

    private static string EscapeExecArgument(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "\"\"";
        }

        if (value.All(static character =>
                !char.IsWhiteSpace(character) &&
                character is not '"' and not '\'' and not '\\' and not '$' and not '`'))
        {
            return value;
        }

        return "\"" + value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal)
            .Replace("$", "\\$", StringComparison.Ordinal)
            .Replace("`", "\\`", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal) + "\"";
    }
}
