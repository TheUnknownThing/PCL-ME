using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Frontend.Avalonia.Workflows;

namespace PCL.Core.Backend.Test.App;

[TestClass]
[DoNotParallelize]
public sealed class FrontendRuntimePathsTest
{
    [TestMethod]
    public void Resolve_MigratesLegacySharedConfigJson()
    {
        using var environment = new FrontendRuntimePathTestEnvironment();
        File.WriteAllText(
            Path.Combine(environment.SharedDataDirectory, "config.json"),
            """
            {
              "SystemDebugMode": true
            }
            """);

        var paths = FrontendRuntimePaths.Resolve(new FrontendPlatformAdapter());
        var provider = paths.OpenSharedConfigProvider();

        Assert.IsTrue(File.Exists(paths.SharedConfigPath));
        Assert.IsTrue(provider.Get<bool>("SystemDebugMode"));
    }

    [TestMethod]
    public void Resolve_MigratesLegacyLocalSetupIni()
    {
        using var environment = new FrontendRuntimePathTestEnvironment();
        File.WriteAllText(
            Path.Combine(environment.DataDirectory, "setup.ini"),
            """
            UiLauncherTransparent:456
            """);

        var paths = FrontendRuntimePaths.Resolve(new FrontendPlatformAdapter());
        var provider = paths.OpenLocalConfigProvider();

        Assert.IsTrue(File.Exists(paths.LocalConfigPath));
        Assert.AreEqual(456, provider.Get<int>("UiLauncherTransparent"));
    }

    [TestMethod]
    public void OpenInstanceConfigProvider_MigratesLegacyUppercaseSetupIni()
    {
        var root = CreateTempDirectory();
        try
        {
            var instanceDirectory = Path.Combine(root, "instance");
            var pclDirectory = Path.Combine(instanceDirectory, "PCL");
            Directory.CreateDirectory(pclDirectory);
            File.WriteAllText(
                Path.Combine(pclDirectory, "Setup.ini"),
                """
                VersionCache:abc
                """);

            var provider = FrontendRuntimePaths.OpenInstanceConfigProvider(instanceDirectory);
            var configPath = Path.Combine(pclDirectory, "config.v1.yml");

            Assert.IsTrue(File.Exists(configPath));
            Assert.AreEqual("abc", provider.Get<string>("VersionCache"));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void OpenInstanceConfigProvider_WithoutCreatingDirectory_DoesNotCreatePclDirectory()
    {
        var root = CreateTempDirectory();
        try
        {
            var instanceDirectory = Path.Combine(root, "instance");
            Directory.CreateDirectory(instanceDirectory);

            _ = FrontendRuntimePaths.OpenInstanceConfigProvider(instanceDirectory, createDirectoryIfMissing: false);

            Assert.IsFalse(Directory.Exists(Path.Combine(instanceDirectory, "PCL")));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void ResolveCurrentLauncherLogFilePath_PrefersMostRecentLauncherLog()
    {
        using var environment = new FrontendRuntimePathTestEnvironment();
        var paths = FrontendRuntimePaths.Resolve(new FrontendPlatformAdapter());
        Directory.CreateDirectory(paths.LauncherLogDirectory);

        var legacyLogPath = Path.Combine(paths.LauncherLogDirectory, "PCL.log");
        var timestampedLogPath = Path.Combine(paths.LauncherLogDirectory, "PCL-123456.log");
        File.WriteAllText(legacyLogPath, "legacy");
        File.WriteAllText(timestampedLogPath, "current");
        File.SetLastWriteTimeUtc(legacyLogPath, DateTime.UtcNow.AddMinutes(-5));
        File.SetLastWriteTimeUtc(timestampedLogPath, DateTime.UtcNow);

        Assert.AreEqual(timestampedLogPath, paths.ResolveCurrentLauncherLogFilePath());
    }

    [TestMethod]
    public void OpenSharedConfigProvider_RecreatesInvalidDirectoryTarget()
    {
        using var environment = new FrontendRuntimePathTestEnvironment();
        var paths = FrontendRuntimePaths.Resolve(new FrontendPlatformAdapter());

        Directory.CreateDirectory(paths.SharedConfigPath);

        var provider = paths.OpenSharedConfigProvider();
        provider.Set("SystemDebugMode", true);
        provider.Sync();

        Assert.IsTrue(File.Exists(paths.SharedConfigPath));
        Assert.IsTrue(paths.MigrationWarnings.Count > 0);
        Assert.IsTrue(Directory.EnumerateFileSystemEntries(environment.SharedDataDirectory, "config.v1.json.invalid-*").Any());
    }

    [TestMethod]
    public void OpenLocalConfigProvider_RecreatesInvalidDirectoryTarget()
    {
        using var environment = new FrontendRuntimePathTestEnvironment();
        var paths = FrontendRuntimePaths.Resolve(new FrontendPlatformAdapter());

        Directory.CreateDirectory(paths.LocalConfigPath);

        var provider = paths.OpenLocalConfigProvider();
        provider.Set("UiLauncherTransparent", 456);
        provider.Sync();

        Assert.IsTrue(File.Exists(paths.LocalConfigPath));
        Assert.IsTrue(paths.MigrationWarnings.Count > 0);
        Assert.IsTrue(Directory.EnumerateFileSystemEntries(environment.DataDirectory, "config.v1.yml.invalid-*").Any());
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "pcl-runtime-paths-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private sealed class FrontendRuntimePathTestEnvironment : IDisposable
    {
        private readonly Dictionary<string, string?> _originalValues = new(StringComparer.Ordinal);

        public FrontendRuntimePathTestEnvironment()
        {
            RootDirectory = CreateTempDirectory();
            DataDirectory = Path.Combine(RootDirectory, "data");
            SharedDataDirectory = Path.Combine(RootDirectory, "shared");
            SharedLocalDataDirectory = Path.Combine(RootDirectory, "shared-local");
            TempDirectory = Path.Combine(RootDirectory, "temp");

            Directory.CreateDirectory(DataDirectory);
            Directory.CreateDirectory(SharedDataDirectory);
            Directory.CreateDirectory(SharedLocalDataDirectory);
            Directory.CreateDirectory(TempDirectory);

            SetEnvironmentVariable("PCL_PATH", DataDirectory);
            SetEnvironmentVariable("PCL_PATH_SHARED", SharedDataDirectory);
            SetEnvironmentVariable("PCL_PATH_LOCAL", SharedLocalDataDirectory);
            SetEnvironmentVariable("PCL_PATH_TEMP", TempDirectory);
            SetEnvironmentVariable("PCL_PORTABLE", "0");
            SetEnvironmentVariable("HOME", RootDirectory);
            SetEnvironmentVariable("USERPROFILE", RootDirectory);
        }

        public string RootDirectory { get; }
        public string DataDirectory { get; }
        public string SharedDataDirectory { get; }
        public string SharedLocalDataDirectory { get; }
        public string TempDirectory { get; }

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
