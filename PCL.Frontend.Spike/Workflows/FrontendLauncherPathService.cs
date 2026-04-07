namespace PCL.Frontend.Spike.Workflows;

internal static class FrontendLauncherPathService
{
    private static readonly string[] KnownCommandScriptExtensions = [".bat", ".cmd", ".command", ".sh"];

    public static string DefaultLauncherFolderRaw =>
        OperatingSystem.IsWindows() ? "$.minecraft\\" : "$.minecraft/";

    public static string GetLatestLaunchScriptFileName(FrontendPlatformAdapter platformAdapter)
    {
        ArgumentNullException.ThrowIfNull(platformAdapter);
        return $"LatestLaunch{platformAdapter.GetCommandScriptExtension()}";
    }

    public static string GetLatestLaunchScriptPath(
        FrontendRuntimePaths runtimePaths,
        FrontendPlatformAdapter platformAdapter)
    {
        ArgumentNullException.ThrowIfNull(runtimePaths);
        return Path.Combine(
            runtimePaths.LauncherAppDataDirectory,
            GetLatestLaunchScriptFileName(platformAdapter));
    }

    public static IEnumerable<string> EnumerateLatestLaunchScriptPaths(
        string directory,
        FrontendPlatformAdapter platformAdapter)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        ArgumentNullException.ThrowIfNull(platformAdapter);

        var currentExtension = platformAdapter.GetCommandScriptExtension();
        yield return Path.Combine(directory, $"LatestLaunch{currentExtension}");

        foreach (var extension in KnownCommandScriptExtensions)
        {
            if (string.Equals(extension, currentExtension, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            yield return Path.Combine(directory, $"LatestLaunch{extension}");
        }
    }

    public static string ResolveLauncherFolder(string? rawValue, FrontendRuntimePaths runtimePaths)
    {
        ArgumentNullException.ThrowIfNull(runtimePaths);

        var effectiveRawValue = string.IsNullOrWhiteSpace(rawValue)
            ? DefaultLauncherFolderRaw
            : rawValue.Trim();
        var resolvedPath = ResolvePath(effectiveRawValue, runtimePaths, normalizeSeparators: true);

        if (OperatingSystem.IsWindows())
        {
            return resolvedPath;
        }

        var legacyResolvedPath = ResolvePath(effectiveRawValue, runtimePaths, normalizeSeparators: false);
        if (string.Equals(legacyResolvedPath, resolvedPath, StringComparison.Ordinal))
        {
            return resolvedPath;
        }

        if (TryMigrateLegacyDirectory(legacyResolvedPath, resolvedPath))
        {
            return resolvedPath;
        }

        return Directory.Exists(resolvedPath) || !Directory.Exists(legacyResolvedPath)
            ? resolvedPath
            : legacyResolvedPath;
    }

    private static string ResolvePath(string rawValue, FrontendRuntimePaths runtimePaths, bool normalizeSeparators)
    {
        var expanded = rawValue.Replace(
            "$",
            EnsureTrailingSeparator(runtimePaths.ExecutableDirectory),
            StringComparison.Ordinal);
        if (normalizeSeparators)
        {
            expanded = NormalizeSeparators(expanded);
        }

        return Path.GetFullPath(expanded);
    }

    private static string NormalizeSeparators(string path)
    {
        return OperatingSystem.IsWindows()
            ? path.Replace('/', '\\')
            : path.Replace('\\', '/');
    }

    private static string EnsureTrailingSeparator(string path)
    {
        return path.EndsWith(Path.DirectorySeparatorChar) || path.EndsWith(Path.AltDirectorySeparatorChar)
            ? path
            : path + Path.DirectorySeparatorChar;
    }

    private static bool TryMigrateLegacyDirectory(string legacyPath, string targetPath)
    {
        if (!Directory.Exists(legacyPath) || Directory.Exists(targetPath))
        {
            return false;
        }

        try
        {
            Directory.Move(legacyPath, targetPath);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
