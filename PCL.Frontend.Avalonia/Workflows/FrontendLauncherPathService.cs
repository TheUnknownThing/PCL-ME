namespace PCL.Frontend.Avalonia.Workflows;

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

        if (HasLauncherPayload(legacyResolvedPath) && !HasLauncherPayload(resolvedPath))
        {
            if (TryMigrateLegacyDirectory(legacyResolvedPath, resolvedPath))
            {
                return resolvedPath;
            }

            return legacyResolvedPath;
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
        if (!Directory.Exists(legacyPath))
        {
            return false;
        }

        try
        {
            if (!Directory.Exists(targetPath))
            {
                Directory.Move(legacyPath, targetPath);
                return true;
            }

            MergeDirectoryContents(legacyPath, targetPath);

            if (!Directory.EnumerateFileSystemEntries(legacyPath).Any())
            {
                Directory.Delete(legacyPath, false);
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool HasLauncherPayload(string path)
    {
        if (!Directory.Exists(path))
        {
            return false;
        }

        return Directory.Exists(Path.Combine(path, "versions")) ||
               Directory.Exists(Path.Combine(path, "libraries")) ||
               Directory.Exists(Path.Combine(path, "assets")) ||
               File.Exists(Path.Combine(path, "launcher_profiles.json"));
    }

    private static void MergeDirectoryContents(string sourcePath, string targetPath)
    {
        Directory.CreateDirectory(targetPath);

        foreach (var directory in Directory.EnumerateDirectories(sourcePath))
        {
            var name = Path.GetFileName(directory);
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            var targetDirectory = Path.Combine(targetPath, name);
            if (Directory.Exists(targetDirectory))
            {
                MergeDirectoryContents(directory, targetDirectory);
                if (!Directory.EnumerateFileSystemEntries(directory).Any())
                {
                    Directory.Delete(directory, false);
                }
                continue;
            }

            Directory.Move(directory, targetDirectory);
        }

        foreach (var file in Directory.EnumerateFiles(sourcePath))
        {
            var name = Path.GetFileName(file);
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            var targetFile = Path.Combine(targetPath, name);
            if (File.Exists(targetFile))
            {
                continue;
            }

            File.Move(file, targetFile);
        }
    }
}
