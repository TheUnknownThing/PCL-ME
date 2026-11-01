namespace PCL.Frontend.Avalonia.Workflows;

internal static class FrontendLauncherPathService
{
    private static readonly string[] KnownCommandScriptExtensions = [".bat", ".cmd", ".command", ".sh"];

    public static string DefaultLauncherFolderRaw =>
        OperatingSystem.IsWindows() ? "$.minecraft\\" : "~/.minecraft/";

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

    public static void EnsureLauncherFolderLayout(string launcherDirectory)
    {
        if (string.IsNullOrWhiteSpace(launcherDirectory))
        {
            return;
        }

        try
        {
            Directory.CreateDirectory(launcherDirectory);
            Directory.CreateDirectory(Path.Combine(launcherDirectory, "versions"));

            var launcherProfilesPath = Path.Combine(launcherDirectory, "launcher_profiles.json");
            if (!File.Exists(launcherProfilesPath) && !Directory.Exists(launcherProfilesPath))
            {
                File.WriteAllText(launcherProfilesPath, "{}");
            }
        }
        catch
        {
            // Best effort: an unwritable user-selected path should not block the shell from loading.
        }
    }

    public static string ResolveLauncherFolder(string? rawValue, FrontendRuntimePaths runtimePaths)
    {
        ArgumentNullException.ThrowIfNull(runtimePaths);

        var effectiveRawValue = string.IsNullOrWhiteSpace(rawValue)
            ? DefaultLauncherFolderRaw
            : rawValue.Trim();
        var resolvedPath = NormalizeMacAppBundleLauncherDirectory(
            ResolvePath(effectiveRawValue, runtimePaths, normalizeSeparators: true),
            runtimePaths);

        if (OperatingSystem.IsWindows())
        {
            return resolvedPath;
        }

        var legacyResolvedPath = NormalizeMacAppBundleLauncherDirectory(
            ResolvePath(effectiveRawValue, runtimePaths, normalizeSeparators: false),
            runtimePaths);
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
        var expanded = ExpandHomeDirectory(rawValue).Replace(
            "$",
            EnsureTrailingSeparator(runtimePaths.ExecutableDirectory),
            StringComparison.Ordinal);
        if (normalizeSeparators)
        {
            expanded = NormalizeSeparators(expanded);
        }

        return Path.GetFullPath(expanded);
    }

    private static string ExpandHomeDirectory(string rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue) || rawValue[0] != '~')
        {
            return rawValue;
        }

        var homeDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (string.IsNullOrWhiteSpace(homeDirectory))
        {
            return rawValue;
        }

        if (rawValue.Length == 1)
        {
            return homeDirectory;
        }

        var nextChar = rawValue[1];
        if (nextChar != Path.DirectorySeparatorChar && nextChar != Path.AltDirectorySeparatorChar)
        {
            return rawValue;
        }

        return Path.Combine(homeDirectory, rawValue[2..]);
    }

    private static string NormalizeSeparators(string path)
    {
        return OperatingSystem.IsWindows()
            ? path.Replace('/', '\\')
            : path.Replace('\\', '/');
    }

    private static string NormalizeMacAppBundleLauncherDirectory(string path, FrontendRuntimePaths runtimePaths)
    {
        if (!OperatingSystem.IsMacOS())
        {
            return path;
        }

        var homeDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (string.IsNullOrWhiteSpace(homeDirectory) ||
            !TryGetMacBundleMacOsDirectory(runtimePaths.ExecutableDirectory, out var macOsDirectory))
        {
            return path;
        }

        var bundleLauncherDirectory = TrimTrailingSeparators(Path.Combine(macOsDirectory, ".minecraft"));
        return PathsEqual(path, bundleLauncherDirectory)
            ? Path.Combine(homeDirectory, ".minecraft")
            : path;
    }

    private static string EnsureTrailingSeparator(string path)
    {
        return path.EndsWith(Path.DirectorySeparatorChar) || path.EndsWith(Path.AltDirectorySeparatorChar)
            ? path
            : path + Path.DirectorySeparatorChar;
    }

    private static string TrimTrailingSeparators(string path)
    {
        return path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    private static bool PathsEqual(string left, string right)
    {
        return string.Equals(
            TrimTrailingSeparators(Path.GetFullPath(left)),
            TrimTrailingSeparators(Path.GetFullPath(right)),
            StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryGetMacBundleMacOsDirectory(string executableDirectory, out string macOsDirectory)
    {
        macOsDirectory = string.Empty;
        if (string.IsNullOrWhiteSpace(executableDirectory))
        {
            return false;
        }

        var normalizedDirectory = TrimTrailingSeparators(Path.GetFullPath(executableDirectory));
        var contentsDirectory = Path.GetDirectoryName(normalizedDirectory);
        if (string.IsNullOrWhiteSpace(contentsDirectory) ||
            !string.Equals(Path.GetFileName(contentsDirectory), "Contents", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var appDirectory = Path.GetDirectoryName(contentsDirectory);
        if (string.IsNullOrWhiteSpace(appDirectory) ||
            !appDirectory.EndsWith(".app", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.Equals(Path.GetFileName(normalizedDirectory), "MacOS", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        macOsDirectory = normalizedDirectory;
        return true;
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
