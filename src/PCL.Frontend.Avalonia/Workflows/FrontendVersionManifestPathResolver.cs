namespace PCL.Frontend.Avalonia.Workflows;

internal static class FrontendVersionManifestPathResolver
{
    public static string GetInstanceDirectory(string launcherFolder, string versionName)
    {
        return Path.Combine(launcherFolder, "versions", versionName);
    }

    public static string? ResolveManifestPath(string launcherFolder, string versionName)
    {
        if (string.IsNullOrWhiteSpace(launcherFolder) || string.IsNullOrWhiteSpace(versionName))
        {
            return null;
        }

        return ResolveManifestPathFromInstanceDirectory(GetInstanceDirectory(launcherFolder, versionName), versionName);
    }

    public static string? ResolveManifestPathFromInstanceDirectory(string instanceDirectory, string instanceName)
    {
        if (string.IsNullOrWhiteSpace(instanceDirectory) || !Directory.Exists(instanceDirectory))
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(instanceName))
        {
            var exactPath = Path.Combine(instanceDirectory, $"{instanceName}.json");
            if (File.Exists(exactPath))
            {
                return exactPath;
            }
        }

        var jsonFiles = Directory.EnumerateFiles(instanceDirectory, "*.json", SearchOption.TopDirectoryOnly)
            .ToArray();
        return jsonFiles.Length == 1 ? jsonFiles[0] : null;
    }
}
