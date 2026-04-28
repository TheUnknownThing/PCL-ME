using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Frontend.Avalonia.Workflows;

namespace PCL.Core.Backend.Test.App;

[TestClass]
public sealed class FrontendStartupScalingServiceTest
{
    [TestMethod]
    public void Resolve_UsesDefaultWhenScaleFactorIsNotStored()
    {
        using var environment = new FrontendStartupScalingEnvironment();

        var result = FrontendStartupScalingService.Resolve(environment.CreateRuntimePaths());

        Assert.IsFalse(result.HasStoredScaleFactor);
        Assert.AreEqual(FrontendStartupScalingService.DefaultUiScaleFactor, result.ScaleFactor);
    }

    [TestMethod]
    public void Resolve_ReadsAndNormalizesStoredScaleFactor()
    {
        using var environment = new FrontendStartupScalingEnvironment();
        File.WriteAllText(
            environment.LocalConfigPath,
            """
            UiScaleFactor: 1.777
            """);

        var result = FrontendStartupScalingService.Resolve(environment.CreateRuntimePaths());

        Assert.IsTrue(result.HasStoredScaleFactor);
        Assert.AreEqual(1.78d, result.ScaleFactor);
    }

    [TestMethod]
    public void Resolve_ReadsStringStoredScaleFactor()
    {
        using var environment = new FrontendStartupScalingEnvironment();
        File.WriteAllText(
            environment.LocalConfigPath,
            """
            UiScaleFactor: "1.8"
            """);

        var result = FrontendStartupScalingService.Resolve(environment.CreateRuntimePaths());

        Assert.IsTrue(result.HasStoredScaleFactor);
        Assert.AreEqual(1.8d, result.ScaleFactor);
    }

    [TestMethod]
    public void ApplyStoredScale_SetsAvaloniaGlobalScaleFactor()
    {
        using var environment = new FrontendStartupScalingEnvironment();
        var previousValue = Environment.GetEnvironmentVariable(FrontendStartupScalingService.GlobalScaleFactorEnvironmentVariable);
        File.WriteAllText(
            environment.LocalConfigPath,
            """
            UiScaleFactor: 1.8
            """);

        try
        {
            FrontendStartupScalingService.ApplyStoredScale(environment.CreateRuntimePaths());

            Assert.AreEqual(
                "1.8",
                Environment.GetEnvironmentVariable(FrontendStartupScalingService.GlobalScaleFactorEnvironmentVariable));
        }
        finally
        {
            Environment.SetEnvironmentVariable(
                FrontendStartupScalingService.GlobalScaleFactorEnvironmentVariable,
                previousValue);
        }
    }

    [TestMethod]
    public void FormatUiScaleFactorLabel_FormatsAsPercentage()
    {
        Assert.AreEqual("180%", FrontendStartupScalingService.FormatUiScaleFactorLabel(1.8d));
    }

    private sealed class FrontendStartupScalingEnvironment : IDisposable
    {
        public FrontendStartupScalingEnvironment()
        {
            RootDirectory = Path.Combine(Path.GetTempPath(), "pcl-startup-scaling-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(RootDirectory);
            SharedDirectory = Path.Combine(RootDirectory, "shared");
            Directory.CreateDirectory(SharedDirectory);
            LocalConfigPath = Path.Combine(RootDirectory, "config.v1.yml");
            SharedConfigPath = Path.Combine(SharedDirectory, "config.v1.json");
        }

        public string RootDirectory { get; }

        public string SharedDirectory { get; }

        public string LocalConfigPath { get; }

        public string SharedConfigPath { get; }

        public FrontendRuntimePaths CreateRuntimePaths()
        {
            return new FrontendRuntimePaths(
                ExecutableDirectory: RootDirectory,
                TempDirectory: Path.Combine(RootDirectory, "temp"),
                DataDirectory: Path.Combine(RootDirectory, "data"),
                SharedConfigDirectory: SharedDirectory,
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
