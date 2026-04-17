using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Frontend.Avalonia.Workflows;

namespace PCL.Core.Backend.Test.App;

[TestClass]
public sealed class FrontendToolsCompositionServiceTest
{
    [TestMethod]
    [DataRow("zh-Hans")]
    [DataRow("zh-Hant")]
    [DataRow("lzh")]
    public void LoadHelpState_UsesChineseFallbackForChineseLocales(string locale)
    {
        using var environment = new HelpCompositionTestEnvironment();

        var helpState = FrontendToolsCompositionService.LoadHelpState(
            environment.CreateRuntimePaths(),
            locale);

        Assert.IsTrue(helpState.Entries.Any(entry => entry.Title == "自定义帮助页面"));
        Assert.IsFalse(helpState.Entries.Any(entry => entry.Title == "Custom Help Pages"));
    }

    [TestMethod]
    public void LoadHelpState_UsesEnglishHelpForNonChineseLocales()
    {
        using var environment = new HelpCompositionTestEnvironment();

        var helpState = FrontendToolsCompositionService.LoadHelpState(
            environment.CreateRuntimePaths(),
            "en-US");

        Assert.IsTrue(helpState.Entries.Any(entry => entry.Title == "Custom Help Pages"));
        Assert.IsFalse(helpState.Entries.Any(entry => entry.Title == "自定义帮助页面"));
    }

    private sealed class HelpCompositionTestEnvironment : IDisposable
    {
        public HelpCompositionTestEnvironment()
        {
            RootDirectory = Path.Combine(Path.GetTempPath(), "pcl-help-compose-test-" + Guid.NewGuid().ToString("N"));
            DataDirectory = Path.Combine(RootDirectory, "data");
            SharedDirectory = Path.Combine(RootDirectory, "shared");
            TempDirectory = Path.Combine(RootDirectory, "temp");
            LauncherAppDataDirectory = Path.Combine(RootDirectory, "launcher-appdata");

            Directory.CreateDirectory(DataDirectory);
            Directory.CreateDirectory(SharedDirectory);
            Directory.CreateDirectory(TempDirectory);
            Directory.CreateDirectory(LauncherAppDataDirectory);
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
            if (Directory.Exists(RootDirectory))
            {
                Directory.Delete(RootDirectory, recursive: true);
            }
        }
    }
}
