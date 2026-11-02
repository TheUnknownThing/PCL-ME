using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Frontend.Avalonia.Workflows;

namespace PCL.Core.Backend.Test.App;

[TestClass]
public sealed class FrontendLauncherPathServiceTest
{
    [TestMethod]
    public void ResolveLauncherFolder_UsesHomeMinecraftByDefaultOnNonWindows()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var environment = new LauncherFolderTestEnvironment();
        var runtimePaths = CreateRuntimePaths(Path.Combine(environment.RootDirectory, "PCL-ME.app", "Contents", "MacOS"));

        var resolvedPath = FrontendLauncherPathService.ResolveLauncherFolder(null, runtimePaths);

        Assert.AreEqual(
            Path.GetFullPath(Path.Combine(environment.RootDirectory, ".minecraft")).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            Path.GetFullPath(resolvedPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
    }

    [TestMethod]
    public void ResolveLauncherFolder_ReroutesLegacyMacBundleMinecraftPath()
    {
        if (!OperatingSystem.IsMacOS())
        {
            return;
        }

        using var environment = new LauncherFolderTestEnvironment();
        var executableDirectory = Path.Combine(environment.RootDirectory, "Applications", "PCL-ME.app", "Contents", "MacOS");
        Directory.CreateDirectory(Path.Combine(executableDirectory, ".minecraft"));
        var runtimePaths = CreateRuntimePaths(executableDirectory);

        var resolvedPath = FrontendLauncherPathService.ResolveLauncherFolder("$.minecraft/", runtimePaths);

        Assert.AreEqual(
            Path.GetFullPath(Path.Combine(environment.RootDirectory, ".minecraft")).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            Path.GetFullPath(resolvedPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
    }

    [TestMethod]
    public void EnsureLauncherFolderLayout_CreatesVersionsDirectoryAndLauncherProfiles()
    {
        using var environment = new LauncherFolderTestEnvironment();
        var launcherDirectory = Path.Combine(environment.RootDirectory, ".minecraft");

        FrontendLauncherPathService.EnsureLauncherFolderLayout(launcherDirectory);

        Assert.IsTrue(Directory.Exists(launcherDirectory));
        Assert.IsTrue(Directory.Exists(Path.Combine(launcherDirectory, "versions")));
        Assert.AreEqual("{}", File.ReadAllText(Path.Combine(launcherDirectory, "launcher_profiles.json")));
    }

    private static FrontendRuntimePaths CreateRuntimePaths(string executableDirectory)
    {
        return new FrontendRuntimePaths(
            executableDirectory,
            Path.Combine(Path.GetTempPath(), "pcl-launcher-paths-test-temp"),
            Path.Combine(Path.GetTempPath(), "pcl-launcher-paths-test-data"),
            Path.Combine(Path.GetTempPath(), "pcl-launcher-paths-test-shared"),
            Path.Combine(Path.GetTempPath(), "pcl-launcher-paths-test-shared", "config.v1.json"),
            Path.Combine(Path.GetTempPath(), "pcl-launcher-paths-test-data", "config.v1.yml"),
            Path.Combine(Path.GetTempPath(), "pcl-launcher-paths-test-appdata"),
            []);
    }

    private sealed class LauncherFolderTestEnvironment : IDisposable
    {
        private readonly Dictionary<string, string?> _originalValues = new(StringComparer.Ordinal);

        public LauncherFolderTestEnvironment()
        {
            RootDirectory = Path.Combine(Path.GetTempPath(), "pcl-launcher-paths-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(RootDirectory);
            SetEnvironmentVariable("HOME", RootDirectory);
            SetEnvironmentVariable("USERPROFILE", RootDirectory);
        }

        public string RootDirectory { get; }

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
