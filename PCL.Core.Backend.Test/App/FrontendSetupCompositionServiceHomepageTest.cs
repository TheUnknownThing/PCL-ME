using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.App.Essentials;
using PCL.Core.Testing;
using PCL.Frontend.Avalonia.Workflows;

namespace PCL.Core.Backend.Test.App;

[TestClass]
public sealed class FrontendSetupCompositionServiceHomepageTest
{
    [TestMethod]
    public void Compose_UsesPclMeAnnouncementAsDefaultHomepagePreset()
    {
        using var environment = new FrontendSetupEnvironment();

        var result = FrontendSetupCompositionService.Compose(environment.CreateRuntimePaths(), new DictionaryI18nService());

        Assert.AreEqual(11, result.Ui.HomepagePresetIndex);
        Assert.AreEqual(0, result.Ui.HomepageTypeIndex);
    }

    [TestMethod]
    public void Compose_MapsStoredHomepageTypeValuesToDisplayIndex()
    {
        using var environment = new FrontendSetupEnvironment();
        File.WriteAllText(
            environment.LocalConfigPath,
            """
            UiCustomType: 2
            UiCustomNet: https://example.com/homepage.xaml
            UiCustomPreset: 7
            """);

        var result = FrontendSetupCompositionService.Compose(environment.CreateRuntimePaths(), new DictionaryI18nService());

        Assert.AreEqual(3, result.Ui.HomepageTypeIndex);
        Assert.AreEqual("https://example.com/homepage.xaml", result.Ui.HomepageUrl);
        Assert.AreEqual(7, result.Ui.HomepagePresetIndex);
    }

    [TestMethod]
    public void Compose_UsesGoldCompatibleBackgroundDefaults()
    {
        using var environment = new FrontendSetupEnvironment();

        var result = FrontendSetupCompositionService.Compose(environment.CreateRuntimePaths(), new DictionaryI18nService());

        Assert.AreEqual(1000d, result.Ui.BackgroundOpacity);
        Assert.AreEqual(0d, result.Ui.BackgroundBlur);
        Assert.AreEqual(0, result.Ui.BackgroundSuitIndex);
        Assert.IsTrue(result.Ui.BackgroundColorful);
    }

    [TestMethod]
    public void Compose_ReadsStoredBackgroundSettings()
    {
        using var environment = new FrontendSetupEnvironment();
        File.WriteAllText(
            environment.LocalConfigPath,
            """
            UiBackgroundOpacity: 640
            UiBackgroundBlur: 12
            UiBackgroundSuit: 4
            UiBackgroundColorful: false
            """);

        var result = FrontendSetupCompositionService.Compose(environment.CreateRuntimePaths(), new DictionaryI18nService());

        Assert.AreEqual(640d, result.Ui.BackgroundOpacity);
        Assert.AreEqual(12d, result.Ui.BackgroundBlur);
        Assert.AreEqual(4, result.Ui.BackgroundSuitIndex);
        Assert.IsFalse(result.Ui.BackgroundColorful);
    }

    [TestMethod]
    public void ComposeActiveSurface_RefreshesLaunchWithoutRebuildingJava()
    {
        using var environment = new FrontendSetupEnvironment();
        const string initialJavaPath = "/tmp/java-8/bin/java";
        const string updatedJavaPath = "/tmp/java-17/bin/java";
        File.WriteAllText(
            environment.LocalConfigPath,
            $$"""
            LaunchArgumentTitle: Old Title
            LaunchArgumentJavaUser: '[{"Path":"{{initialJavaPath}}","IsEnable":true}]'
            """);

        var runtimePaths = environment.CreateRuntimePaths();
        var i18n = new DictionaryI18nService();
        var initial = FrontendSetupCompositionService.Compose(runtimePaths, i18n);

        File.WriteAllText(
            environment.LocalConfigPath,
            $$"""
            LaunchArgumentTitle: New Title
            LaunchArgumentJavaUser: '[{"Path":"{{updatedJavaPath}}","IsEnable":true}]'
            """);

        var updated = FrontendSetupCompositionService.ComposeActiveSurface(
            runtimePaths,
            i18n,
            initial,
            LauncherFrontendSubpageKey.SetupLaunch);

        Assert.AreEqual("New Title", updated.Launch.WindowTitle);
        Assert.AreSame(initial.Java, updated.Java);
        Assert.AreEqual(Path.GetFullPath(initialJavaPath), updated.Java.Entries.Single().Key);
    }

    [TestMethod]
    public void ComposeActiveSurface_RefreshesJavaWithoutRebuildingLaunch()
    {
        using var environment = new FrontendSetupEnvironment();
        const string initialJavaPath = "/tmp/java-8/bin/java";
        const string updatedJavaPath = "/tmp/java-17/bin/java";
        File.WriteAllText(
            environment.LocalConfigPath,
            $$"""
            LaunchArgumentTitle: Old Title
            LaunchArgumentJavaUser: '[{"Path":"{{initialJavaPath}}","IsEnable":true}]'
            """);

        var runtimePaths = environment.CreateRuntimePaths();
        var i18n = new DictionaryI18nService();
        var initial = FrontendSetupCompositionService.Compose(runtimePaths, i18n);

        File.WriteAllText(
            environment.LocalConfigPath,
            $$"""
            LaunchArgumentTitle: New Title
            LaunchArgumentJavaUser: '[{"Path":"{{updatedJavaPath}}","IsEnable":true}]'
            """);

        var updated = FrontendSetupCompositionService.ComposeActiveSurface(
            runtimePaths,
            i18n,
            initial,
            LauncherFrontendSubpageKey.SetupJava);

        Assert.AreSame(initial.Launch, updated.Launch);
        Assert.AreEqual("Old Title", updated.Launch.WindowTitle);
        Assert.AreEqual(Path.GetFullPath(updatedJavaPath), updated.Java.Entries.Single().Key);
    }

    private sealed class FrontendSetupEnvironment : IDisposable
    {
        public FrontendSetupEnvironment()
        {
            RootDirectory = Path.Combine(Path.GetTempPath(), "pcl-setup-homepage-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(RootDirectory);
            LocalConfigPath = Path.Combine(RootDirectory, "config.v1.yml");
            SharedConfigPath = Path.Combine(RootDirectory, "shared", "config.v1.json");
        }

        public string RootDirectory { get; }

        public string LocalConfigPath { get; }

        public string SharedConfigPath { get; }

        public FrontendRuntimePaths CreateRuntimePaths()
        {
            return new FrontendRuntimePaths(
                ExecutableDirectory: RootDirectory,
                TempDirectory: Path.Combine(RootDirectory, "temp"),
                DataDirectory: RootDirectory,
                SharedConfigDirectory: Path.Combine(RootDirectory, "shared"),
                SharedConfigPath: SharedConfigPath,
                LocalConfigPath: LocalConfigPath,
                LauncherAppDataDirectory: Path.Combine(RootDirectory, "launcher"),
                MigrationWarnings: []);
        }

        public void Dispose()
        {
            Directory.Delete(RootDirectory, recursive: true);
        }
    }
}
