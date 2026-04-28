using System.Text;

namespace PCL.Frontend.Avalonia.Workflows;

internal static class FrontendLinuxDesktopEntryService
{
    public static FrontendDesktopEntryOperationResult RegisterCurrentProcess()
    {
        if (!OperatingSystem.IsLinux())
        {
            return FrontendDesktopEntryOperationResult.Fail("Desktop entry registration is only supported on Linux.");
        }

        var executablePath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            return FrontendDesktopEntryOperationResult.Fail("Could not determine the current executable path.");
        }

        return Register(executablePath);
    }

    public static FrontendDesktopEntryOperationResult Register(string executablePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);

        if (!OperatingSystem.IsLinux())
        {
            return FrontendDesktopEntryOperationResult.Fail("Desktop entry registration is only supported on Linux.");
        }

        if (!File.Exists(executablePath))
        {
            return FrontendDesktopEntryOperationResult.Fail($"Executable does not exist: {executablePath}");
        }

        var applicationsDirectory = ResolveApplicationsDirectory();
        if (string.IsNullOrWhiteSpace(applicationsDirectory))
        {
            return FrontendDesktopEntryOperationResult.Fail("Could not resolve the XDG applications directory.");
        }

        Directory.CreateDirectory(applicationsDirectory);

        var desktopEntryPath = GetDesktopEntryPath(applicationsDirectory);
        var desktopEntryContent = BuildDesktopEntry(
            FrontendApplicationIdentity.DisplayName,
            [executablePath],
            ResolveIconPath(executablePath),
            Path.GetDirectoryName(executablePath));

        var wasChanged = WriteIfChanged(desktopEntryPath, desktopEntryContent);
        return FrontendDesktopEntryOperationResult.Success(
            desktopEntryPath,
            wasChanged
                ? $"Registered desktop entry: {desktopEntryPath}"
                : $"Desktop entry is already registered: {desktopEntryPath}");
    }

    public static FrontendDesktopEntryOperationResult Unregister()
    {
        if (!OperatingSystem.IsLinux())
        {
            return FrontendDesktopEntryOperationResult.Fail("Desktop entry registration is only supported on Linux.");
        }

        var applicationsDirectory = ResolveApplicationsDirectory();
        if (string.IsNullOrWhiteSpace(applicationsDirectory))
        {
            return FrontendDesktopEntryOperationResult.Fail("Could not resolve the XDG applications directory.");
        }

        var desktopEntryPath = GetDesktopEntryPath(applicationsDirectory);
        if (!File.Exists(desktopEntryPath))
        {
            return FrontendDesktopEntryOperationResult.Success(
                desktopEntryPath,
                $"Desktop entry was not registered: {desktopEntryPath}");
        }

        File.Delete(desktopEntryPath);
        return FrontendDesktopEntryOperationResult.Success(
            desktopEntryPath,
            $"Unregistered desktop entry: {desktopEntryPath}");
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
        builder.AppendLine("Comment=PCL2 Multiplatform Edition");
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

        foreach (var iconPath in GetIconPathCandidates(executableDirectory))
        {
            if (File.Exists(iconPath))
            {
                return iconPath;
            }
        }

        return null;
    }

    private static IEnumerable<string> GetIconPathCandidates(string executableDirectory)
    {
        yield return Path.Combine(executableDirectory, FrontendApplicationIdentity.LinuxPackageIconFileName);
        yield return Path.Combine(executableDirectory, FrontendApplicationIdentity.LinuxIconRelativePath);
    }

    private static string GetDesktopEntryPath(string applicationsDirectory)
    {
        return Path.Combine(
            applicationsDirectory,
            FrontendApplicationIdentity.LinuxDesktopFileId + ".desktop");
    }

    private static bool WriteIfChanged(string path, string content)
    {
        if (File.Exists(path))
        {
            var existing = File.ReadAllText(path);
            if (string.Equals(existing, content, StringComparison.Ordinal))
            {
                return false;
            }
        }

        File.WriteAllText(path, content, new UTF8Encoding(false));
        return true;
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

internal sealed record FrontendDesktopEntryOperationResult(
    bool IsSuccess,
    string? DesktopEntryPath,
    string Message)
{
    public static FrontendDesktopEntryOperationResult Success(string desktopEntryPath, string message) =>
        new(true, desktopEntryPath, message);

    public static FrontendDesktopEntryOperationResult Fail(string message) =>
        new(false, DesktopEntryPath: null, message);
}
