using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.App;
using PCL.Core.App.Essentials;
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

    [TestMethod]
    public void Compose_WithFollowLauncherProxy_ForwardsCustomProxyCredentialsToJvmArguments()
    {
        using var environment = new LaunchCompositionTestEnvironment();
        var launcherDirectory = Path.Combine(environment.RootDirectory, ".minecraft");
        var instanceName = "ProxyInstance";
        var instanceDirectory = Path.Combine(launcherDirectory, "versions", instanceName);
        Directory.CreateDirectory(instanceDirectory);

        File.WriteAllText(
            Path.Combine(instanceDirectory, $"{instanceName}.json"),
            """
            {
              "id": "ProxyInstance",
              "mainClass": "net.minecraft.client.main.Main",
              "libraries": [],
              "downloads": {},
              "assetIndex": {
                "id": "legacy",
                "url": "https://example.invalid/assets/indexes/legacy.json"
              }
            }
            """);

        var runtimePaths = environment.CreateRuntimePaths();
        var localConfig = runtimePaths.OpenLocalConfigProvider();
        localConfig.Set("LaunchFolderSelect", launcherDirectory);
        localConfig.Set("LaunchInstanceSelect", instanceName);
        localConfig.Set("LaunchAdvanceJvm", "-XX:+UseG1GC");
        localConfig.Sync();

        var instanceConfig = FrontendRuntimePaths.OpenInstanceConfigProvider(instanceDirectory);
        instanceConfig.Set("VersionAdvanceUseProxyV2", true);
        instanceConfig.Sync();

        var sharedConfig = runtimePaths.OpenSharedConfigProvider();
        sharedConfig.Set("SystemHttpProxyType", 2);
        sharedConfig.Set("SystemHttpProxy", ProtectSharedValue(runtimePaths, "http://proxy.example:8080"));
        sharedConfig.Set("SystemHttpProxyCustomUsername", ProtectSharedValue(runtimePaths, "proxy-user"));
        sharedConfig.Set("SystemHttpProxyCustomPassword", ProtectSharedValue(runtimePaths, "proxy-pass"));
        sharedConfig.Sync();

        var result = FrontendLaunchCompositionService.Compose(
            new AvaloniaCommandOptions("test", ForceCjkFontWarning: false),
            runtimePaths);

        StringAssert.Contains(result.ArgumentPlan.FinalArguments, "-Dhttp.proxyHost=proxy.example");
        StringAssert.Contains(result.ArgumentPlan.FinalArguments, "-Dhttp.proxyPort=8080");
        StringAssert.Contains(result.ArgumentPlan.FinalArguments, "-Dhttp.proxyUser=proxy-user");
        StringAssert.Contains(result.ArgumentPlan.FinalArguments, "-Dhttp.proxyPassword=proxy-pass");
        StringAssert.Contains(result.ArgumentPlan.FinalArguments, "-Dhttps.proxyUser=proxy-user");
        StringAssert.Contains(result.ArgumentPlan.FinalArguments, "-Dhttps.proxyPassword=proxy-pass");
    }

    private static string ProtectSharedValue(FrontendRuntimePaths runtimePaths, string value)
    {
        var encryptionKey = LauncherSharedEncryptionKeyService.ResolveOrCreate(
            runtimePaths.SharedConfigDirectory,
            Environment.GetEnvironmentVariable("PCL_ENCRYPTION_KEY"));
        return LauncherDataProtectionService.Protect(value, encryptionKey);
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

            SetEnvironmentVariable("PCL_ENCRYPTION_KEY", "frontend-launch-compose-test-key");
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
