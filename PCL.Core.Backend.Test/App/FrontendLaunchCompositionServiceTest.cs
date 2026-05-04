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
            CreateOptions(),
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
            CreateOptions(),
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
            CreateOptions(),
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

    [TestMethod]
    public void Compose_WithInstanceOverride_UsesCommandLineSelection()
    {
        using var environment = new LaunchCompositionTestEnvironment();
        var launcherDirectory = Path.Combine(environment.RootDirectory, ".minecraft");
        var localConfig = environment.CreateRuntimePaths().OpenLocalConfigProvider();
        localConfig.Set("LaunchFolderSelect", launcherDirectory);
        localConfig.Set("LaunchInstanceSelect", "ConfigInstance");
        localConfig.Sync();

        Directory.CreateDirectory(Path.Combine(launcherDirectory, "versions", "ConfigInstance"));
        Directory.CreateDirectory(Path.Combine(launcherDirectory, "versions", "CliInstance"));

        var runtimePaths = environment.CreateRuntimePaths();
        var result = FrontendLaunchCompositionService.Compose(
            CreateOptions(instanceNameOverride: "CliInstance"),
            runtimePaths);

        Assert.AreEqual("CliInstance", result.InstanceName);
        Assert.AreEqual("CliInstance", result.PrecheckRequest.InstanceName);
    }

    [TestMethod]
    public void Compose_WithInheritedManifest_UsesParentAndChildManifestData()
    {
        using var environment = new LaunchCompositionTestEnvironment();
        var launcherDirectory = Path.Combine(environment.RootDirectory, ".minecraft");
        var parentName = "ParentInstance";
        var childName = "ChildInstance";
        var parentDirectory = Path.Combine(launcherDirectory, "versions", parentName);
        var childDirectory = Path.Combine(launcherDirectory, "versions", childName);
        Directory.CreateDirectory(parentDirectory);
        Directory.CreateDirectory(childDirectory);

        File.WriteAllText(
            Path.Combine(parentDirectory, $"{parentName}.json"),
            """
            {
              "id": "ParentInstance",
              "mainClass": "parent.Main",
              "arguments": {
                "game": ["--parentArg"],
                "jvm": ["-Dparent=true"]
              },
              "libraries": [
                {
                  "name": "com.example:parent:1.0",
                  "downloads": {
                    "artifact": {
                      "path": "com/example/parent/1.0/parent-1.0.jar",
                      "url": "https://example.invalid/parent.jar",
                      "sha1": "parent-sha1"
                    }
                  }
                }
              ],
              "downloads": {
                "client": {
                  "url": "https://example.invalid/parent-client.jar",
                  "sha1": "parent-client-sha1"
                }
              }
            }
            """);
        File.WriteAllText(
            Path.Combine(childDirectory, $"{childName}.json"),
            """
            {
              "id": "ChildInstance",
              "inheritsFrom": "ParentInstance",
              "mainClass": "child.Main",
              "arguments": {
                "game": ["--childArg"],
                "jvm": ["-Dchild=true"]
              },
              "libraries": [
                {
                  "name": "com.example:child:1.0",
                  "downloads": {
                    "artifact": {
                      "path": "com/example/child/1.0/child-1.0.jar",
                      "url": "https://example.invalid/child.jar",
                      "sha1": "child-sha1"
                    }
                  }
                }
              ],
              "downloads": {
                "client": {
                  "url": "https://example.invalid/child-client.jar",
                  "sha1": "child-client-sha1"
                }
              }
            }
            """);

        var runtimePaths = environment.CreateRuntimePaths();
        var localConfig = runtimePaths.OpenLocalConfigProvider();
        localConfig.Set("LaunchFolderSelect", launcherDirectory);
        localConfig.Set("LaunchInstanceSelect", childName);
        localConfig.Sync();

        var result = FrontendLaunchCompositionService.Compose(
            CreateOptions(),
            runtimePaths);

        CollectionAssert.Contains(
            result.ClasspathPlan.Entries.ToList(),
            Path.Combine(launcherDirectory, "libraries", "com", "example", "parent", "1.0", "parent-1.0.jar"));
        CollectionAssert.Contains(
            result.ClasspathPlan.Entries.ToList(),
            Path.Combine(launcherDirectory, "libraries", "com", "example", "child", "1.0", "child-1.0.jar"));
        Assert.IsTrue(result.RequiredArtifacts.Any(requirement => requirement.DownloadUrl == "https://example.invalid/parent-client.jar"));
        Assert.IsTrue(result.RequiredArtifacts.Any(requirement => requirement.DownloadUrl == "https://example.invalid/child-client.jar"));
        StringAssert.Contains(result.ArgumentPlan.FinalArguments, "-Dparent=true");
        StringAssert.Contains(result.ArgumentPlan.FinalArguments, "-Dchild=true");
        StringAssert.Contains(result.ArgumentPlan.FinalArguments, "--parentArg");
        StringAssert.Contains(result.ArgumentPlan.FinalArguments, "--childArg");
        StringAssert.Contains(result.ArgumentPlan.FinalArguments, "child.Main");
    }

    private static AvaloniaCommandOptions CreateOptions(string scenario = "test", string? instanceNameOverride = null)
    {
        return new AvaloniaCommandOptions(
            AvaloniaCommandKind.App,
            scenario,
            ForceCjkFontWarning: false,
            InstanceNameOverride: instanceNameOverride);
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
