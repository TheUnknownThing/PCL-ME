using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.App;
using Special = System.Environment.SpecialFolder;

namespace PCL.Core.Test;

[TestClass]
public class AppPathLayoutTest
{
    [TestMethod]
    public void Constructor_CreatesExpectedDirectories()
    {
        var root = CreateTempDirectory();
        try
        {
            var env = new FakeAppEnvironment(
                Path.Combine(root, "app"),
                Path.Combine(root, "temp"),
                new Dictionary<Special, string>
                {
                    [Special.ApplicationData] = Path.Combine(root, "roaming"),
                    [Special.LocalApplicationData] = Path.Combine(root, "local")
                });

            var layout = new AppPathLayout(env, "PCLME_Test", ".PCLME_Test", enableDebugOverrides: false);

            Assert.AreEqual(Path.Combine(root, "app"), layout.DefaultDirectory);
            Assert.AreEqual(Path.Combine(root, "app", "PCL"), layout.PortableData);
            Assert.AreEqual(Path.Combine(root, "local", "PCLME_Test", "PCL"), layout.Data);
            Assert.IsFalse(layout.UsesPortableDataDirectory);
            Assert.AreEqual(Path.Combine(root, "roaming", "PCLME_Test"), layout.SharedData);
            Assert.AreEqual(Path.Combine(root, "local", "PCLME_Test"), layout.SharedLocalData);
            Assert.AreEqual(Path.Combine(root, "temp", "PCLME_Test"), layout.Temp);
            Assert.AreEqual(Path.Combine(root, "roaming", ".PCLME_Test"), layout.OldSharedData);
            Assert.IsTrue(Directory.Exists(layout.Data));
            Assert.IsTrue(Directory.Exists(layout.SharedData));
            Assert.IsTrue(Directory.Exists(layout.SharedLocalData));
            Assert.IsTrue(Directory.Exists(layout.Temp));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [TestMethod]
    public void Constructor_UsesPortableData_WhenPortableMarkerExists()
    {
        var root = CreateTempDirectory();
        try
        {
            var appDirectory = Path.Combine(root, "app");
            Directory.CreateDirectory(appDirectory);
            File.WriteAllText(Path.Combine(appDirectory, "PCL.portable"), string.Empty);

            var env = new FakeAppEnvironment(
                appDirectory,
                Path.Combine(root, "temp"),
                new Dictionary<Special, string>
                {
                    [Special.ApplicationData] = Path.Combine(root, "roaming"),
                    [Special.LocalApplicationData] = Path.Combine(root, "local")
                });

            var layout = new AppPathLayout(env, "PCLME_Test", ".PCLME_Test", enableDebugOverrides: false);

            Assert.AreEqual(Path.Combine(appDirectory, "PCL"), layout.Data);
            Assert.IsTrue(layout.UsesPortableDataDirectory);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [TestMethod]
    public void Constructor_UsesUserScopedData_WhenLegacyPortablePayloadExistsWithoutPortableMarker()
    {
        var root = CreateTempDirectory();
        try
        {
            var appDirectory = Path.Combine(root, "app");
            var portableDataDirectory = Path.Combine(appDirectory, "PCL");
            Directory.CreateDirectory(portableDataDirectory);
            File.WriteAllText(Path.Combine(portableDataDirectory, "config.v1.yml"), "SystemDebugAnim: 0");

            var env = new FakeAppEnvironment(
                appDirectory,
                Path.Combine(root, "temp"),
                new Dictionary<Special, string>
                {
                    [Special.ApplicationData] = Path.Combine(root, "roaming"),
                    [Special.LocalApplicationData] = Path.Combine(root, "local")
                });

            var layout = new AppPathLayout(env, "PCLME_Test", ".PCLME_Test", enableDebugOverrides: false);

            Assert.AreEqual(Path.Combine(root, "local", "PCLME_Test", "PCL"), layout.Data);
            Assert.IsFalse(layout.UsesPortableDataDirectory);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [TestMethod]
    public void Constructor_UsesDebugOverrides_WhenEnabled()
    {
        var root = CreateTempDirectory();
        try
        {
            var env = new FakeAppEnvironment(
                Path.Combine(root, "app"),
                Path.Combine(root, "temp"),
                new Dictionary<Special, string>
                {
                    [Special.ApplicationData] = Path.Combine(root, "roaming"),
                    [Special.LocalApplicationData] = Path.Combine(root, "local")
                },
                new Dictionary<string, string>
                {
                    ["PCL_PATH"] = Path.Combine(root, "override-data"),
                    ["PCL_PATH_SHARED"] = Path.Combine(root, "override-shared"),
                    ["PCL_PATH_LOCAL"] = Path.Combine(root, "override-local"),
                    ["PCL_PATH_TEMP"] = Path.Combine(root, "override-temp")
                });

            var layout = new AppPathLayout(env, "PCLME_Test", ".PCLME_Test", enableDebugOverrides: true);

            Assert.AreEqual(Path.Combine(root, "override-data"), layout.Data);
            Assert.AreEqual(Path.Combine(root, "override-shared"), layout.SharedData);
            Assert.AreEqual(Path.Combine(root, "override-local"), layout.SharedLocalData);
            Assert.AreEqual(Path.Combine(root, "override-temp"), layout.Temp);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "pcl-foundation-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private sealed class FakeAppEnvironment(
        string defaultDirectory,
        string tempPath,
        IReadOnlyDictionary<Special, string> specialFolders,
        IReadOnlyDictionary<string, string>? variables = null) : IAppEnvironment
    {
        public string DefaultDirectory { get; } = defaultDirectory;
        public string TempPath { get; } = tempPath;

        public string GetFolderPath(Special folder) => specialFolders[folder];

        public string? GetEnvironmentVariable(string key)
        {
            if (variables == null) return null;
            return variables.TryGetValue(key, out var value) ? value : null;
        }
    }
}
