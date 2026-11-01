using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Frontend.Avalonia.Workflows;

namespace PCL.Core.Backend.Test.App;

[TestClass]
public sealed class FrontendStartupVisualCompositionServiceTest
{
    [TestMethod]
    public void Compose_EnablesSplashScreenByDefault()
    {
        using var environment = new FrontendStartupVisualEnvironment();
        var runtimePaths = environment.CreateRuntimePaths();

        var result = FrontendStartupVisualCompositionService.Compose(runtimePaths);

        Assert.IsTrue(result.ShouldShowSplashScreen);
        Assert.AreEqual(@"Images\icon.ico", result.SplashScreen?.IconPath);
    }

    [TestMethod]
    public void Compose_DisablesSplashScreenWhenUiLauncherLogoIsFalse()
    {
        using var environment = new FrontendStartupVisualEnvironment();
        File.WriteAllText(
            environment.LocalConfigPath,
            """
            UiLauncherLogo: false
            """);

        var result = FrontendStartupVisualCompositionService.Compose(environment.CreateRuntimePaths());

        Assert.IsFalse(result.ShouldShowSplashScreen);
        Assert.IsNull(result.SplashScreen);
    }

    private sealed class FrontendStartupVisualEnvironment : IDisposable
    {
        public FrontendStartupVisualEnvironment()
        {
            RootDirectory = Path.Combine(Path.GetTempPath(), "pcl-startup-visual-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(RootDirectory);
            LocalConfigPath = Path.Combine(RootDirectory, "config.v1.yml");
        }

        public string RootDirectory { get; }

        public string LocalConfigPath { get; }

        public FrontendRuntimePaths CreateRuntimePaths()
        {
            return new FrontendRuntimePaths(
                ExecutableDirectory: RootDirectory,
                TempDirectory: Path.Combine(RootDirectory, "temp"),
                DataDirectory: RootDirectory,
                SharedConfigDirectory: Path.Combine(RootDirectory, "shared"),
                SharedConfigPath: Path.Combine(RootDirectory, "shared", "config.v1.json"),
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
