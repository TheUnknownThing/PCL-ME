namespace PCL.Frontend.Avalonia.Workflows;

internal static class FrontendSettingsSnapshotWorkflowService
{
    private const string ExportDirectoryName = "config-exports";
    private const string TimestampFormat = "yyyyMMdd-HHmmss";

    public static string GetSnapshotRootDirectory(FrontendRuntimePaths runtimePaths)
    {
        ArgumentNullException.ThrowIfNull(runtimePaths);
        return Path.Combine(runtimePaths.FrontendArtifactDirectory, ExportDirectoryName);
    }

    public static string CreateSnapshot(FrontendRuntimePaths runtimePaths, DateTimeOffset? timestamp = null)
    {
        ArgumentNullException.ThrowIfNull(runtimePaths);

        EnsureConfigFilesExist(runtimePaths);

        var exportDirectory = CreateUniqueExportDirectory(
            GetSnapshotRootDirectory(runtimePaths),
            timestamp ?? DateTimeOffset.Now);
        CopyConfigFile(
            runtimePaths.SharedConfigPath,
            Path.Combine(exportDirectory, Path.GetFileName(runtimePaths.SharedConfigPath)));
        CopyConfigFile(
            runtimePaths.LocalConfigPath,
            Path.Combine(exportDirectory, Path.GetFileName(runtimePaths.LocalConfigPath)));
        return exportDirectory;
    }

    public static void RestoreSnapshot(FrontendRuntimePaths runtimePaths, string snapshotDirectory)
    {
        ArgumentNullException.ThrowIfNull(runtimePaths);
        ArgumentException.ThrowIfNullOrWhiteSpace(snapshotDirectory);

        if (!Directory.Exists(snapshotDirectory))
        {
            throw new DirectoryNotFoundException($"Settings export directory not found: {snapshotDirectory}");
        }

        var sharedSourcePath = Path.Combine(snapshotDirectory, Path.GetFileName(runtimePaths.SharedConfigPath));
        var localSourcePath = Path.Combine(snapshotDirectory, Path.GetFileName(runtimePaths.LocalConfigPath));
        EnsureSnapshotFileExists(sharedSourcePath, "shared launcher config");
        EnsureSnapshotFileExists(localSourcePath, "local launcher config");

        CopyConfigFile(sharedSourcePath, runtimePaths.SharedConfigPath);
        CopyConfigFile(localSourcePath, runtimePaths.LocalConfigPath);
        runtimePaths.ClearConfigProviderCache();
    }

    private static void EnsureConfigFilesExist(FrontendRuntimePaths runtimePaths)
    {
        if (!File.Exists(runtimePaths.SharedConfigPath))
        {
            _ = runtimePaths.OpenSharedConfigProvider();
        }

        if (!File.Exists(runtimePaths.LocalConfigPath))
        {
            var provider = runtimePaths.OpenLocalConfigProvider();
            provider.Sync();
        }
    }

    private static void EnsureSnapshotFileExists(string filePath, string configLabel)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException(
                $"The selected settings export is missing the {configLabel} file.",
                filePath);
        }
    }

    private static string CreateUniqueExportDirectory(string exportRootDirectory, DateTimeOffset timestamp)
    {
        Directory.CreateDirectory(exportRootDirectory);

        var baseDirectoryName = timestamp.LocalDateTime.ToString(TimestampFormat);
        var candidateDirectory = Path.Combine(exportRootDirectory, baseDirectoryName);
        if (!Directory.Exists(candidateDirectory) && !File.Exists(candidateDirectory))
        {
            Directory.CreateDirectory(candidateDirectory);
            return candidateDirectory;
        }

        for (var suffix = 1; ; suffix++)
        {
            candidateDirectory = Path.Combine(exportRootDirectory, $"{baseDirectoryName}-{suffix}");
            if (Directory.Exists(candidateDirectory) || File.Exists(candidateDirectory))
            {
                continue;
            }

            Directory.CreateDirectory(candidateDirectory);
            return candidateDirectory;
        }
    }

    private static void CopyConfigFile(string sourcePath, string targetPath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
        File.Copy(sourcePath, targetPath, overwrite: true);
    }
}
