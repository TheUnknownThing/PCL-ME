using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Frontend.Avalonia.Workflows;

namespace PCL.Core.Backend.Test.App;

[TestClass]
public sealed class FrontendUpdateInstallWorkflowServiceTest
{
    [TestMethod]
    public void ResolveInstallLayout_UsesExecutableDirectoryForPortableInstall()
    {
        var layout = FrontendUpdateInstallWorkflowService.ResolveInstallLayout(
            FrontendUpdateInstallPlatform.Windows,
            "/Applications/PCL-ME",
            "/Applications/PCL-ME/PCL.Frontend.Avalonia.exe");

        Assert.AreEqual(FrontendUpdateInstallTargetKind.Directory, layout.TargetKind);
        Assert.AreEqual("/Applications/PCL-ME", layout.TargetPath);
        Assert.AreEqual("/Applications/PCL-ME/PCL.Frontend.Avalonia.exe", layout.RestartTargetPath);
    }

    [TestMethod]
    public void ResolveInstallLayout_RecognizesMacAppBundle()
    {
        var layout = FrontendUpdateInstallWorkflowService.ResolveInstallLayout(
            FrontendUpdateInstallPlatform.MacOS,
            "/Applications/PCL-ME.app/Contents/MacOS",
            "/Applications/PCL-ME.app/Contents/MacOS/PCL.Frontend.Avalonia");

        Assert.AreEqual(FrontendUpdateInstallTargetKind.MacAppBundle, layout.TargetKind);
        Assert.AreEqual("/Applications/PCL-ME.app", layout.TargetPath);
        Assert.AreEqual("/Applications/PCL-ME.app", layout.RestartTargetPath);
    }

    [TestMethod]
    public void ResolveExtractedPackageRoot_PrefersSingleTopLevelDirectory()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "pcl-update-test-" + Guid.NewGuid().ToString("N"));
        var packageRoot = Path.Combine(tempRoot, "PCL-ME-win-x64");
        Directory.CreateDirectory(packageRoot);

        try
        {
            var resolved = FrontendUpdateInstallWorkflowService.ResolveExtractedPackageRoot(
                FrontendUpdateInstallPlatform.Windows,
                tempRoot,
                "PCL-ME");

            Assert.AreEqual(packageRoot, resolved);
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [TestMethod]
    public void ResolveExtractedPackageRoot_FindsNestedMacAppBundle()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "pcl-update-test-" + Guid.NewGuid().ToString("N"));
        var packageRoot = Path.Combine(tempRoot, "PCL-ME-macos-arm64", "PCL-ME.app");
        Directory.CreateDirectory(packageRoot);

        try
        {
            var resolved = FrontendUpdateInstallWorkflowService.ResolveExtractedPackageRoot(
                FrontendUpdateInstallPlatform.MacOS,
                tempRoot,
                "PCL-ME.app");

            Assert.AreEqual(packageRoot, resolved);
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [TestMethod]
    public void BuildInstallerScript_WindowsUsesRobocopyAndRestartsLauncher()
    {
        var script = FrontendUpdateInstallWorkflowService.BuildInstallerScript(
            FrontendUpdateInstallPlatform.Windows,
            new FrontendUpdateInstallLayout(
                FrontendUpdateInstallTargetKind.Directory,
                "C:\\PCL-ME",
                "C:\\PCL-ME",
                "C:\\PCL-ME\\PCL.Frontend.Avalonia.exe",
                "PCL-ME"),
            "C:\\Temp\\Extracted",
            processId: 4321);

        StringAssert.Contains(script, "robocopy");
        StringAssert.Contains(script, "/MIR");
        StringAssert.Contains(script, "PID=4321");
        StringAssert.Contains(script, "PCL.Frontend.Avalonia.exe");
    }

    [TestMethod]
    public void BuildInstallerScript_UnixDirectoryClearsTargetBeforeCopying()
    {
        var script = FrontendUpdateInstallWorkflowService.BuildInstallerScript(
            FrontendUpdateInstallPlatform.Linux,
            new FrontendUpdateInstallLayout(
                FrontendUpdateInstallTargetKind.Directory,
                "/opt/pcl",
                "/opt/pcl",
                "/opt/pcl/PCL.Frontend.Avalonia",
                "pcl"),
            "/tmp/pcl-update",
            processId: 4321);

        StringAssert.Contains(script, "rm -rf \"$TARGET\"");
        StringAssert.Contains(script, "mkdir -p \"$TARGET\"");
        StringAssert.Contains(script, "cp -R \"$SOURCE\"/. \"$TARGET\"/");
    }
}
