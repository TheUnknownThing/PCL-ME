using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Frontend.Avalonia.Workflows;

namespace PCL.Core.Backend.Test.App;

[TestClass]
public sealed class FrontendSettingsSnapshotWorkflowServiceTest
{
    [TestMethod]
    public void CreateSnapshot_WritesSharedAndLocalConfigsIntoTimestampedDirectory()
    {
        using var workspace = new TempSettingsWorkspace();
        var timestamp = CreateLocalTimestamp(2026, 4, 17, 12, 34, 56);
        File.WriteAllText(workspace.RuntimePaths.SharedConfigPath, "{\n  \"SystemLocale\": \"en-US\"\n}");
        File.WriteAllText(workspace.RuntimePaths.LocalConfigPath, "UiBlur: true\n");

        var exportDirectory = FrontendSettingsSnapshotWorkflowService.CreateSnapshot(workspace.RuntimePaths, timestamp);

        StringAssert.EndsWith(exportDirectory, Path.Combine("config-exports", "20260417-123456"));
        Assert.AreEqual(
            File.ReadAllText(workspace.RuntimePaths.SharedConfigPath),
            File.ReadAllText(Path.Combine(exportDirectory, Path.GetFileName(workspace.RuntimePaths.SharedConfigPath))));
        Assert.AreEqual(
            File.ReadAllText(workspace.RuntimePaths.LocalConfigPath),
            File.ReadAllText(Path.Combine(exportDirectory, Path.GetFileName(workspace.RuntimePaths.LocalConfigPath))));
    }

    [TestMethod]
    public void CreateSnapshot_CreatesMissingConfigFilesBeforeExporting()
    {
        using var workspace = new TempSettingsWorkspace();

        var exportDirectory = FrontendSettingsSnapshotWorkflowService.CreateSnapshot(
            workspace.RuntimePaths,
            CreateLocalTimestamp(2026, 4, 17, 12, 35, 0));

        Assert.IsTrue(File.Exists(workspace.RuntimePaths.SharedConfigPath));
        Assert.IsTrue(File.Exists(workspace.RuntimePaths.LocalConfigPath));
        Assert.IsTrue(File.Exists(Path.Combine(exportDirectory, Path.GetFileName(workspace.RuntimePaths.SharedConfigPath))));
        Assert.IsTrue(File.Exists(Path.Combine(exportDirectory, Path.GetFileName(workspace.RuntimePaths.LocalConfigPath))));
    }

    [TestMethod]
    public void RestoreSnapshot_RestoresSharedAndLocalConfigsTogether()
    {
        using var workspace = new TempSettingsWorkspace();
        File.WriteAllText(workspace.RuntimePaths.SharedConfigPath, "{\n  \"SystemLocale\": \"en-US\"\n}");
        File.WriteAllText(workspace.RuntimePaths.LocalConfigPath, "UiBlur: true\n");
        var exportDirectory = FrontendSettingsSnapshotWorkflowService.CreateSnapshot(
            workspace.RuntimePaths,
            CreateLocalTimestamp(2026, 4, 17, 12, 36, 0));

        File.WriteAllText(workspace.RuntimePaths.SharedConfigPath, "{\n  \"SystemLocale\": \"zh-Hans\"\n}");
        File.WriteAllText(workspace.RuntimePaths.LocalConfigPath, "UiBlur: false\n");

        FrontendSettingsSnapshotWorkflowService.RestoreSnapshot(workspace.RuntimePaths, exportDirectory);

        Assert.AreEqual("{\n  \"SystemLocale\": \"en-US\"\n}", File.ReadAllText(workspace.RuntimePaths.SharedConfigPath));
        Assert.AreEqual("UiBlur: true\n", File.ReadAllText(workspace.RuntimePaths.LocalConfigPath));
    }

    [TestMethod]
    public void RestoreSnapshot_RejectsIncompleteExportDirectory()
    {
        using var workspace = new TempSettingsWorkspace();
        var exportDirectory = Path.Combine(workspace.RootPath, "incomplete-export");
        Directory.CreateDirectory(exportDirectory);
        File.WriteAllText(
            Path.Combine(exportDirectory, Path.GetFileName(workspace.RuntimePaths.SharedConfigPath)),
            "{\n  \"SystemLocale\": \"en-US\"\n}");

        var exception = Assert.ThrowsExactly<FileNotFoundException>(() =>
            FrontendSettingsSnapshotWorkflowService.RestoreSnapshot(workspace.RuntimePaths, exportDirectory));

        StringAssert.Contains(exception.Message, "local launcher config");
    }

    private static DateTimeOffset CreateLocalTimestamp(int year, int month, int day, int hour, int minute, int second)
    {
        var localTime = new DateTime(year, month, day, hour, minute, second, DateTimeKind.Unspecified);
        return new DateTimeOffset(localTime, TimeZoneInfo.Local.GetUtcOffset(localTime));
    }

    private sealed class TempSettingsWorkspace : IDisposable
    {
        public TempSettingsWorkspace()
        {
            RootPath = Directory.CreateTempSubdirectory("pcl-settings-snapshot-").FullName;
            var tempDirectory = Path.Combine(RootPath, "temp");
            var dataDirectory = Path.Combine(RootPath, "data");
            var sharedDirectory = Path.Combine(RootPath, "shared");
            var launcherDirectory = Path.Combine(RootPath, "launcher");
            Directory.CreateDirectory(tempDirectory);
            Directory.CreateDirectory(dataDirectory);
            Directory.CreateDirectory(sharedDirectory);
            Directory.CreateDirectory(launcherDirectory);
            RuntimePaths = new FrontendRuntimePaths(
                ExecutableDirectory: RootPath,
                TempDirectory: tempDirectory,
                DataDirectory: dataDirectory,
                SharedConfigDirectory: sharedDirectory,
                SharedConfigPath: Path.Combine(sharedDirectory, "config.v1.json"),
                LocalConfigPath: Path.Combine(dataDirectory, "config.v1.yml"),
                LauncherAppDataDirectory: launcherDirectory,
                MigrationWarnings: []);
        }

        public string RootPath { get; }

        public FrontendRuntimePaths RuntimePaths { get; }

        public void Dispose()
        {
            if (Directory.Exists(RootPath))
            {
                Directory.Delete(RootPath, recursive: true);
            }
        }
    }
}
