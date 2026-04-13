using PCL.Core.App;
using PCL.Core.App.Configuration;
using PCL.Core.App.Configuration.Storage;

namespace PCL.Frontend.Avalonia.Workflows;

internal sealed record FrontendRuntimePaths(
    string ExecutableDirectory,
    string TempDirectory,
    string DataDirectory,
    string SharedConfigDirectory,
    string SharedConfigPath,
    string LocalConfigPath,
    string LauncherAppDataDirectory,
    IReadOnlyList<string> MigrationWarnings)
{
    public string FrontendArtifactDirectory => Path.Combine(DataDirectory, "frontend-artifacts");

    public string FrontendTempDirectory => Path.Combine(TempDirectory, "frontend-artifacts");

    public string LauncherProfileDirectory => SharedConfigDirectory;

    public string LauncherLogDirectory => Path.Combine(LauncherAppDataDirectory, "Log");

    public JsonFileProvider OpenSharedConfigProvider()
    {
        return OpenProvider(
            SharedConfigPath,
            "shared launcher config",
            path => new JsonFileProvider(path));
    }

    public YamlFileProvider OpenLocalConfigProvider()
    {
        return OpenProvider(
            LocalConfigPath,
            "local launcher config",
            path => new YamlFileProvider(path));
    }

    public static YamlFileProvider OpenInstanceConfigProvider(string instanceDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instanceDirectory);

        var pclDirectory = Path.Combine(instanceDirectory, "PCL");
        var configPath = Path.Combine(pclDirectory, "config.v1.yml");
        TryMigrateLegacyInstanceConfig(pclDirectory, configPath);
        Directory.CreateDirectory(pclDirectory);
        return new YamlFileProvider(configPath);
    }

    public string? ResolveCurrentLauncherLogFilePath()
    {
        return EnumerateLauncherLogFilePaths(LauncherLogDirectory).FirstOrDefault();
    }

    public static IEnumerable<string> EnumerateLauncherLogFilePaths(string logDirectory)
    {
        if (string.IsNullOrWhiteSpace(logDirectory) || !Directory.Exists(logDirectory))
        {
            yield break;
        }

        foreach (var path in Directory.EnumerateFiles(logDirectory, "PCL*.log", SearchOption.TopDirectoryOnly)
                     .OrderByDescending(File.GetLastWriteTimeUtc)
                     .ThenByDescending(Path.GetFileName, StringComparer.OrdinalIgnoreCase))
        {
            yield return path;
        }
    }

    public static FrontendRuntimePaths Resolve(FrontendPlatformAdapter platformAdapter)
    {
        ArgumentNullException.ThrowIfNull(platformAdapter);

        var layout = CreateLayout();
        var migrationWarnings = new List<string>();
        var sharedConfigPath = Path.Combine(layout.SharedData, "config.v1.json");
        var localConfigPath = Path.Combine(layout.Data, "config.v1.yml");
        TryMigrateLegacySharedConfig(layout, sharedConfigPath, migrationWarnings);
        TryMigrateLegacyLocalConfig(layout, localConfigPath, migrationWarnings);
        return new FrontendRuntimePaths(
            Path.GetDirectoryName(Environment.ProcessPath!) ?? Environment.CurrentDirectory,
            layout.Temp,
            layout.Data,
            layout.SharedData,
            sharedConfigPath,
            localConfigPath,
            platformAdapter.GetLauncherAppDataDirectory(),
            migrationWarnings);
    }

    private static AppPathLayout CreateLayout()
    {
#if DEBUG
        return new AppPathLayout(SystemAppEnvironment.Current, "PCLCE_Debug", ".PCLCEDebug", enableDebugOverrides: true);
#else
        return new AppPathLayout(SystemAppEnvironment.Current, "PCLCE", ".PCLCE", enableDebugOverrides: false);
#endif
    }

    private static void TryMigrateLegacySharedConfig(
        AppPathLayout layout,
        string targetSharedConfigPath,
        ICollection<string> migrationWarnings)
    {
        if (File.Exists(targetSharedConfigPath))
        {
            return;
        }

        TryMigrateConfig(
            targetSharedConfigPath,
            [
                new ConfigMigration
                {
                    From = Path.Combine(layout.OldSharedData, "Config.json"),
                    To = targetSharedConfigPath,
                    OnMigration = CopyConfigFile
                },
                new ConfigMigration
                {
                    From = Path.Combine(layout.SharedData, "config.json"),
                    To = targetSharedConfigPath,
                    OnMigration = CopyConfigFile
                }
            ],
            migrationWarnings,
            "shared launcher config");
    }

    private static void TryMigrateLegacyLocalConfig(
        AppPathLayout layout,
        string targetLocalConfigPath,
        ICollection<string> migrationWarnings)
    {
        if (File.Exists(targetLocalConfigPath))
        {
            return;
        }

        var migrations = new List<ConfigMigration>();
        var legacyPortableConfigPath = Path.Combine(layout.PortableData, "config.v1.yml");
        if (!PathsEqual(targetLocalConfigPath, legacyPortableConfigPath))
        {
            migrations.Add(new ConfigMigration
            {
                From = legacyPortableConfigPath,
                To = targetLocalConfigPath,
                OnMigration = CopyConfigFile
            });
        }

        migrations.Add(new ConfigMigration
        {
            From = Path.Combine(layout.Data, "setup.ini"),
            To = targetLocalConfigPath,
            OnMigration = ConvertCatIniToYaml
        });

        if (!layout.UsesPortableDataDirectory)
        {
            migrations.Add(new ConfigMigration
            {
                From = Path.Combine(layout.PortableData, "setup.ini"),
                To = targetLocalConfigPath,
                OnMigration = ConvertCatIniToYaml
            });
        }

        TryMigrateConfig(targetLocalConfigPath, migrations, migrationWarnings, "local launcher config");
    }

    private static void TryMigrateLegacyInstanceConfig(string pclDirectory, string targetConfigPath)
    {
        if (File.Exists(targetConfigPath))
        {
            return;
        }

        TryMigrateConfig(
            targetConfigPath,
            [
                new ConfigMigration
                {
                    From = Path.Combine(pclDirectory, "setup.ini"),
                    To = targetConfigPath,
                    OnMigration = ConvertCatIniToYaml
                },
                new ConfigMigration
                {
                    From = Path.Combine(pclDirectory, "Setup.ini"),
                    To = targetConfigPath,
                    OnMigration = ConvertCatIniToYaml
                }
            ]);
    }

    private static void TryMigrateConfig(
        string targetConfigPath,
        IEnumerable<ConfigMigration> migrations,
        ICollection<string>? migrationWarnings = null,
        string? migrationLabel = null)
    {
        var migrationList = migrations as IReadOnlyList<ConfigMigration> ?? migrations.ToArray();
        try
        {
            ConfigMigration.Migrate(targetConfigPath, migrationList);
        }
        catch (Exception ex)
        {
            migrationWarnings?.Add(BuildMigrationWarning(
                migrationLabel ?? "launcher config",
                targetConfigPath,
                migrationList,
                ex));
        }
    }

    private static string BuildMigrationWarning(
        string migrationLabel,
        string targetConfigPath,
        IReadOnlyList<ConfigMigration> migrations,
        Exception exception)
    {
        var sourcePaths = migrations
            .Select(migration => migration.From)
            .Where(File.Exists)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var sourceSummary = sourcePaths.Length == 0
            ? "an old config file"
            : string.Join(", ", sourcePaths);

        return $"启动器配置迁移失败：{migrationLabel} 在 {sourceSummary} 向 {targetConfigPath}.{Environment.NewLine}迁移产生错误: {exception.Message}";
    }

    private T OpenProvider<T>(string configPath, string configLabel, Func<string, T> factory)
    {
        if (Directory.Exists(configPath))
        {
            var invalidBackupPath = PreserveInvalidConfigPath(configPath);
            AddMigrationWarning(
                $"Failed to open {configLabel} at {configPath}.{Environment.NewLine}" +
                $"The invalid target was preserved at {invalidBackupPath}.{Environment.NewLine}" +
                "Error: config target is a directory, not a file.");
        }

        try
        {
            return factory(configPath);
        }
        catch (ConfigFileInitException ex)
        {
            var invalidBackupPath = PreserveInvalidConfigPath(configPath);
            AddMigrationWarning(
                $"Failed to open {configLabel} at {configPath}.{Environment.NewLine}" +
                $"The invalid target was preserved at {invalidBackupPath}.{Environment.NewLine}" +
                $"Error: {ex.InnerException?.Message ?? ex.Message}");
            return factory(configPath);
        }
    }

    private void AddMigrationWarning(string warning)
    {
        if (MigrationWarnings is List<string> warnings)
        {
            warnings.Add(warning);
        }
    }

    private static string PreserveInvalidConfigPath(string configPath)
    {
        var backupPath = $"{configPath}.invalid-{DateTime.Now:yyyyMMddHHmmssfff}";
        var parentDirectory = Path.GetDirectoryName(configPath);
        if (!string.IsNullOrWhiteSpace(parentDirectory))
        {
            Directory.CreateDirectory(parentDirectory);
        }

        if (Directory.Exists(configPath))
        {
            Directory.Move(configPath, backupPath);
            return backupPath;
        }

        if (File.Exists(configPath))
        {
            File.Move(configPath, backupPath, overwrite: false);
            return backupPath;
        }

        return backupPath;
    }

    private static void CopyConfigFile(string from, string to)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(to)!);
        File.Copy(from, to, overwrite: true);
    }

    private static void ConvertCatIniToYaml(string from, string to)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(to)!);

        var provider = new YamlFileProvider(to);
        foreach (var line in File.ReadLines(from))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var separatorIndex = line.IndexOf(':');
            if (separatorIndex <= 0)
            {
                continue;
            }

            provider.Set(line[..separatorIndex], line[(separatorIndex + 1)..]);
        }

        provider.Sync();
    }

    private static bool PathsEqual(string left, string right)
    {
        var comparison = OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        return string.Equals(
            Path.GetFullPath(left).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            Path.GetFullPath(right).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            comparison);
    }
}
