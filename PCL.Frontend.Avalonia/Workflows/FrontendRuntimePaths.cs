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
    string LauncherAppDataDirectory)
{
    public string FrontendArtifactDirectory => Path.Combine(DataDirectory, "frontend-artifacts");

    public string FrontendTempDirectory => Path.Combine(TempDirectory, "frontend-artifacts");

    public string LauncherProfileDirectory => SharedConfigDirectory;

    public JsonFileProvider OpenSharedConfigProvider()
    {
        return new JsonFileProvider(SharedConfigPath);
    }

    public YamlFileProvider OpenLocalConfigProvider()
    {
        return new YamlFileProvider(LocalConfigPath);
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

    public static FrontendRuntimePaths Resolve(FrontendPlatformAdapter platformAdapter)
    {
        ArgumentNullException.ThrowIfNull(platformAdapter);

        var layout = CreateLayout();
        var sharedConfigPath = Path.Combine(layout.SharedData, "config.v1.json");
        var localConfigPath = Path.Combine(layout.Data, "config.v1.yml");
        TryMigrateLegacySharedConfig(layout, sharedConfigPath);
        TryMigrateLegacyLocalConfig(layout, localConfigPath);
        return new FrontendRuntimePaths(
            Path.GetDirectoryName(Environment.ProcessPath!) ?? Environment.CurrentDirectory,
            layout.Temp,
            layout.Data,
            layout.SharedData,
            sharedConfigPath,
            localConfigPath,
            platformAdapter.GetLauncherAppDataDirectory());
    }

    private static AppPathLayout CreateLayout()
    {
#if DEBUG
        return new AppPathLayout(SystemAppEnvironment.Current, "PCLCE_Debug", ".PCLCEDebug", enableDebugOverrides: true);
#else
        return new AppPathLayout(SystemAppEnvironment.Current, "PCLCE", ".PCLCE", enableDebugOverrides: false);
#endif
    }

    private static void TryMigrateLegacySharedConfig(AppPathLayout layout, string targetSharedConfigPath)
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
            ]);
    }

    private static void TryMigrateLegacyLocalConfig(AppPathLayout layout, string targetLocalConfigPath)
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

        TryMigrateConfig(targetLocalConfigPath, migrations);
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

    private static void TryMigrateConfig(string targetConfigPath, IEnumerable<ConfigMigration> migrations)
    {
        try
        {
            ConfigMigration.Migrate(targetConfigPath, migrations);
        }
        catch
        {
            // Best effort only. The frontend will continue with defaults if migration fails.
        }
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
