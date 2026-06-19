using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Frontend.Avalonia.Workflows;

namespace PCL.Core.Backend.Test.App;

[TestClass]
public sealed class FrontendInstanceCompositionServiceTest
{
    [TestMethod]
    public void Compose_LoadsWorldsAndScreenshotsFromSharedGameDirectory()
    {
        using var environment = new InstanceCompositionTestEnvironment();
        var launcherDirectory = Path.Combine(environment.RootDirectory, ".minecraft");
        var instanceName = "SharedGameDirectory";
        var instanceDirectory = Path.Combine(launcherDirectory, "versions", instanceName);
        Directory.CreateDirectory(instanceDirectory);
        WriteManifest(instanceDirectory, instanceName);
        Directory.CreateDirectory(Path.Combine(launcherDirectory, "saves", "Shared World"));
        Directory.CreateDirectory(Path.Combine(launcherDirectory, "screenshots"));
        File.WriteAllText(Path.Combine(launcherDirectory, "screenshots", "shared.png"), "image");

        var runtimePaths = environment.CreateRuntimePaths();
        var localConfig = runtimePaths.OpenLocalConfigProvider();
        localConfig.Set("LaunchFolderSelect", launcherDirectory);
        localConfig.Set("LaunchInstanceSelect", instanceName);
        localConfig.Set("LaunchArgumentIndieV2", 0);
        localConfig.Sync();

        var composition = FrontendInstanceCompositionService.Compose(
            runtimePaths,
            FrontendInstanceCompositionService.LoadMode.Lightweight);

        Assert.IsFalse(composition.Selection.IsIndie);
        Assert.AreEqual(launcherDirectory, composition.Selection.IndieDirectory);
        Assert.AreEqual("Shared World", composition.World.Entries.Single().Title);
        Assert.AreEqual("shared.png", composition.Screenshot.Entries.Single().Title);
    }

    [TestMethod]
    public void Compose_LoadsWorldsAndScreenshotsFromIsolatedInstanceDirectory()
    {
        using var environment = new InstanceCompositionTestEnvironment();
        var launcherDirectory = Path.Combine(environment.RootDirectory, ".minecraft");
        var instanceName = "IsolatedGameDirectory";
        var instanceDirectory = Path.Combine(launcherDirectory, "versions", instanceName);
        Directory.CreateDirectory(instanceDirectory);
        WriteManifest(instanceDirectory, instanceName);
        Directory.CreateDirectory(Path.Combine(instanceDirectory, "saves", "Isolated World"));
        Directory.CreateDirectory(Path.Combine(instanceDirectory, "screenshots"));
        File.WriteAllText(Path.Combine(instanceDirectory, "screenshots", "isolated.jpg"), "image");

        var runtimePaths = environment.CreateRuntimePaths();
        var localConfig = runtimePaths.OpenLocalConfigProvider();
        localConfig.Set("LaunchFolderSelect", launcherDirectory);
        localConfig.Set("LaunchInstanceSelect", instanceName);
        localConfig.Set("LaunchArgumentIndieV2", 4);
        localConfig.Sync();

        var composition = FrontendInstanceCompositionService.Compose(
            runtimePaths,
            FrontendInstanceCompositionService.LoadMode.Lightweight);

        Assert.IsTrue(composition.Selection.IsIndie);
        Assert.AreEqual(instanceDirectory, composition.Selection.IndieDirectory);
        Assert.AreEqual("Isolated World", composition.World.Entries.Single().Title);
        Assert.AreEqual("isolated.jpg", composition.Screenshot.Entries.Single().Title);
    }

    private static void WriteManifest(string instanceDirectory, string instanceName)
    {
        File.WriteAllText(
            Path.Combine(instanceDirectory, $"{instanceName}.json"),
            $$"""
            {
              "id": "{{instanceName}}",
              "type": "release",
              "mainClass": "net.minecraft.client.main.Main",
              "libraries": [],
              "downloads": {}
            }
            """);
    }

    private sealed class InstanceCompositionTestEnvironment : IDisposable
    {
        private readonly Dictionary<string, string?> _originalValues = new(StringComparer.Ordinal);

        public InstanceCompositionTestEnvironment()
        {
            RootDirectory = Path.Combine(Path.GetTempPath(), "pcl-instance-compose-test-" + Guid.NewGuid().ToString("N"));
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
