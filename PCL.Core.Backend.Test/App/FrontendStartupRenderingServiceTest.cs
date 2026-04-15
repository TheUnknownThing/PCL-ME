using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.App.Configuration.Storage;
using PCL.Frontend.Avalonia.Workflows;

namespace PCL.Core.Backend.Test.App;

[TestClass]
public sealed class FrontendStartupRenderingServiceTest
{
    [TestMethod]
    public void Resolve_ReadsStoredDisableFlagForSupportedPlatform()
    {
        using var environment = new FrontendStartupRenderingEnvironment();
        var provider = new JsonFileProvider(environment.SharedConfigPath);
        provider.Set(FrontendStartupRenderingService.DisableHardwareAccelerationConfigKey, true);
        provider.Sync();

        var result = FrontendStartupRenderingService.Resolve(
            environment.CreateRuntimePaths(),
            FrontendDesktopPlatformKind.Windows);

        Assert.IsTrue(result.IsHardwareAccelerationToggleAvailable);
        Assert.IsTrue(result.DisableHardwareAcceleration);
        Assert.AreEqual(FrontendStartupRenderingMode.Win32Software, result.RenderingMode);
    }

    [TestMethod]
    public void Resolve_IgnoresStoredFlagForUnsupportedPlatform()
    {
        using var environment = new FrontendStartupRenderingEnvironment();
        var provider = new JsonFileProvider(environment.SharedConfigPath);
        provider.Set(FrontendStartupRenderingService.DisableHardwareAccelerationConfigKey, true);
        provider.Sync();

        var result = FrontendStartupRenderingService.Resolve(
            environment.CreateRuntimePaths(),
            FrontendDesktopPlatformKind.Other);

        Assert.IsFalse(result.IsHardwareAccelerationToggleAvailable);
        Assert.IsFalse(result.DisableHardwareAcceleration);
        Assert.AreEqual(FrontendStartupRenderingMode.Default, result.RenderingMode);
    }

    private sealed class FrontendStartupRenderingEnvironment : IDisposable
    {
        public FrontendStartupRenderingEnvironment()
        {
            RootDirectory = Path.Combine(Path.GetTempPath(), "pcl-startup-rendering-test-" + Guid.NewGuid().ToString("N"));
            SharedDirectory = Path.Combine(RootDirectory, "shared");
            Directory.CreateDirectory(SharedDirectory);
            SharedConfigPath = Path.Combine(SharedDirectory, "config.v1.json");
        }

        public string RootDirectory { get; }

        public string SharedDirectory { get; }

        public string SharedConfigPath { get; }

        public FrontendRuntimePaths CreateRuntimePaths()
        {
            return new FrontendRuntimePaths(
                ExecutableDirectory: RootDirectory,
                TempDirectory: Path.Combine(RootDirectory, "temp"),
                DataDirectory: Path.Combine(RootDirectory, "data"),
                SharedConfigDirectory: SharedDirectory,
                SharedConfigPath: SharedConfigPath,
                LocalConfigPath: Path.Combine(RootDirectory, "config.v1.yml"),
                LauncherAppDataDirectory: Path.Combine(RootDirectory, "launcher"),
                MigrationWarnings: []);
        }

        public void Dispose()
        {
            Directory.Delete(RootDirectory, recursive: true);
        }
    }
}
