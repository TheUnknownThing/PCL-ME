using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.Testing;
using PCL.Frontend.Avalonia.Cli;
using PCL.Frontend.Avalonia.Workflows;

namespace PCL.Core.Backend.Test.App;

[TestClass]
public sealed class FrontendLaunchCompositionServiceTest
{
    [TestMethod]
    public void Compose_WithNoSelectedInstance_DoesNotCreateVersionsPclDirectory()
    {
        using var environment = new LaunchCompositionTestEnvironment();
        var launcherDirectory = Path.Combine(environment.RootDirectory, ".minecraft");
        Directory.CreateDirectory(Path.Combine(launcherDirectory, "versions"));

        var runtimePaths = environment.CreateRuntimePaths();
        var localConfig = runtimePaths.OpenLocalConfigProvider();
        localConfig.Set("LaunchFolderSelect", launcherDirectory);
        localConfig.Set("LaunchInstanceSelect", string.Empty);
        localConfig.Sync();

        _ = FrontendLaunchCompositionService.Compose(
            new AvaloniaCommandOptions("test", ForceCjkFontWarning: false),
            runtimePaths);

        Assert.IsFalse(Directory.Exists(Path.Combine(launcherDirectory, "versions", "PCL")));
    }

    [TestMethod]
    public void Compose_WithNoSelectedInstance_UsesLocalizedFallbackName()
    {
        using var environment = new LaunchCompositionTestEnvironment();
        var launcherDirectory = Path.Combine(environment.RootDirectory, ".minecraft");
        Directory.CreateDirectory(Path.Combine(launcherDirectory, "versions"));

        var runtimePaths = environment.CreateRuntimePaths();
        var localConfig = runtimePaths.OpenLocalConfigProvider();
        localConfig.Set("LaunchFolderSelect", launcherDirectory);
        localConfig.Set("LaunchInstanceSelect", string.Empty);
        localConfig.Sync();

        var i18n = new DictionaryI18nService(new Dictionary<string, string>
        {
            ["instance.common.no_selection"] = "未选择实例"
        }, "zh-Hans");

        var result = FrontendLaunchCompositionService.Compose(
            new AvaloniaCommandOptions("test", ForceCjkFontWarning: false),
            runtimePaths,
            i18n: i18n);

        Assert.AreEqual("未选择实例", result.InstanceName);
        Assert.AreEqual("未选择实例", result.PrecheckRequest.InstanceName);
        Assert.AreEqual("未选择实例", result.ReplacementPlan.Values["${version_name}"]);
    }

    private sealed class LaunchCompositionTestEnvironment : IDisposable
    {
        private readonly Dictionary<string, string?> _originalValues = new(StringComparer.Ordinal);

        public LaunchCompositionTestEnvironment()
        {
            RootDirectory = Path.Combine(Path.GetTempPath(), "pcl-launch-compose-test-" + Guid.NewGuid().ToString("N"));
            DataDirectory = Path.Combine(RootDirectory, "data");
            SharedDirectory = Path.Combine(RootDirectory, "shared");
            TempDirectory = Path.Combine(RootDirectory, "temp");
            LauncherAppDataDirectory = Path.Combine(RootDirectory, "launcher-appdata");

            Directory.CreateDirectory(DataDirectory);
            Directory.CreateDirectory(SharedDirectory);
            Directory.CreateDirectory(TempDirectory);
            Directory.CreateDirectory(LauncherAppDataDirectory);

            SetEnvironmentVariable("HOME", RootDirectory);
            SetEnvironmentVariable("USERPROFILE", RootDirectory);
        }

        public string RootDirectory { get; }

        private string DataDirectory { get; }

        private string SharedDirectory { get; }

        private string TempDirectory { get; }

        private string LauncherAppDataDirectory { get; }

        public FrontendRuntimePaths CreateRuntimePaths()
        {
            return new FrontendRuntimePaths(
                ExecutableDirectory: RootDirectory,
                TempDirectory: TempDirectory,
                DataDirectory: DataDirectory,
                SharedConfigDirectory: SharedDirectory,
                SharedConfigPath: Path.Combine(SharedDirectory, "config.v1.json"),
                LocalConfigPath: Path.Combine(DataDirectory, "config.v1.yml"),
                LauncherAppDataDirectory: LauncherAppDataDirectory,
                MigrationWarnings: []);
        }

        public void Dispose()
        {
            foreach (var pair in _originalValues)
            {
                Environment.SetEnvironmentVariable(pair.Key, pair.Value);
            }

            Directory.Delete(RootDirectory, recursive: true);
        }

        private void SetEnvironmentVariable(string key, string value)
        {
            if (!_originalValues.ContainsKey(key))
            {
                _originalValues[key] = Environment.GetEnvironmentVariable(key);
            }

            Environment.SetEnvironmentVariable(key, value);
        }
    }
}
