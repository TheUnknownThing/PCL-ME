using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.App.Essentials;
using PCL.Core.Testing;
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

    [TestMethod]
    public void ComposeActiveSurface_RefreshesToolsTestWithoutRebuildingHelp()
    {
        using var environment = new HelpCompositionTestEnvironment();
        File.WriteAllText(
            environment.SharedConfigPath,
            """
            {
              "CacheDownloadFolder": "/tmp/downloads-one"
            }
            """);

        var runtimePaths = environment.CreateRuntimePaths();
        var i18n = new DictionaryI18nService();
        var initial = FrontendToolsCompositionService.Compose(runtimePaths, i18n);

        var sharedConfig = runtimePaths.OpenSharedConfigProvider();
        sharedConfig.Set("CacheDownloadFolder", "/tmp/downloads-two");
        sharedConfig.Sync();

        var updated = FrontendToolsCompositionService.ComposeActiveSurface(
            runtimePaths,
            i18n,
            initial,
            LauncherFrontendSubpageKey.ToolsTest);

        Assert.AreEqual("/tmp/downloads-two", updated.Test.DownloadFolder);
        Assert.AreSame(initial.Help, updated.Help);
    }

    [TestMethod]
    public void ComposeActiveSurface_RefreshesToolsHelpWithoutRebuildingTest()
    {
        using var environment = new HelpCompositionTestEnvironment();
        var helpFilePath = Path.Combine(environment.DataDirectory, "Help", "en-US", "Guides", "Temporary Guide.json");
        Directory.CreateDirectory(Path.GetDirectoryName(helpFilePath)!);
        File.WriteAllText(
            helpFilePath,
            """
            {
              "Title": "Temporary Guide One",
              "Description": "First version",
              "Keywords": "temporary guide",
              "Types": ["Guides"]
            }
            """);
        File.WriteAllText(
            environment.SharedConfigPath,
            """
            {
              "CacheDownloadFolder": "/tmp/downloads-one"
            }
            """);

        var runtimePaths = environment.CreateRuntimePaths();
        var i18n = new DictionaryI18nService();
        var initial = FrontendToolsCompositionService.Compose(runtimePaths, i18n);

        File.WriteAllText(
            helpFilePath,
            """
            {
              "Title": "Temporary Guide Two",
              "Description": "Second version",
              "Keywords": "temporary guide",
              "Types": ["Guides"]
            }
            """);
        var sharedConfig = runtimePaths.OpenSharedConfigProvider();
        sharedConfig.Set("CacheDownloadFolder", "/tmp/downloads-two");
        sharedConfig.Sync();

        var updated = FrontendToolsCompositionService.ComposeActiveSurface(
            runtimePaths,
            i18n,
            initial,
            LauncherFrontendSubpageKey.ToolsLauncherHelp);

        Assert.IsTrue(updated.Help.Entries.Any(entry => entry.Title == "Temporary Guide Two"));
        Assert.AreSame(initial.Test, updated.Test);
        Assert.AreEqual("/tmp/downloads-one", updated.Test.DownloadFolder);
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

        public string DataDirectory { get; }

        private string SharedDirectory { get; }

        public string SharedConfigPath => Path.Combine(SharedDirectory, "config.v1.json");

        private string TempDirectory { get; }

        private string LauncherAppDataDirectory { get; }

        public FrontendRuntimePaths CreateRuntimePaths()
        {
            return new FrontendRuntimePaths(
                ExecutableDirectory: RootDirectory,
                TempDirectory: TempDirectory,
                DataDirectory: DataDirectory,
                SharedConfigDirectory: SharedDirectory,
                SharedConfigPath: SharedConfigPath,
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
