using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Frontend.Avalonia.Workflows;

namespace PCL.Core.Backend.Test.App;

[TestClass]
public sealed class FrontendUpdateInstallWorkflowServiceTest
{
    [TestMethod]
    public void ResolveInstallLayout_UsesExecutableDirectoryForPortableInstall()
    {
        var executableDirectory = "/Applications/PCL-ME";
        var processPath = "/Applications/PCL-ME/PCL.Frontend.Avalonia.exe";
        var layout = FrontendUpdateInstallWorkflowService.ResolveInstallLayout(
            FrontendUpdateInstallPlatform.Windows,
            executableDirectory,
            processPath);

        Assert.AreEqual(FrontendUpdateInstallTargetKind.Directory, layout.TargetKind);
        Assert.AreEqual(NormalizeForCurrentPlatform(executableDirectory), layout.TargetPath);
        Assert.AreEqual(NormalizeForCurrentPlatform(processPath), layout.RestartTargetPath);
    }

    [TestMethod]
    public void ResolveInstallLayout_RecognizesMacAppBundle()
    {
        var executableDirectory = "/Applications/PCL-ME.app/Contents/MacOS";
        var processPath = "/Applications/PCL-ME.app/Contents/MacOS/PCL.Frontend.Avalonia";
        var layout = FrontendUpdateInstallWorkflowService.ResolveInstallLayout(
            FrontendUpdateInstallPlatform.MacOS,
            executableDirectory,
            processPath);

        Assert.AreEqual(FrontendUpdateInstallTargetKind.MacAppBundle, layout.TargetKind);
        Assert.AreEqual(NormalizeForCurrentPlatform("/Applications/PCL-ME.app"), layout.TargetPath);
        Assert.AreEqual(NormalizeForCurrentPlatform("/Applications/PCL-ME.app"), layout.RestartTargetPath);
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

    private static string NormalizeForCurrentPlatform(string path)
    {
        return Path.GetFullPath(path)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }
}
