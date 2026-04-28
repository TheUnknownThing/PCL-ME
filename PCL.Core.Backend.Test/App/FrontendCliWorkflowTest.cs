using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Frontend.Avalonia.Cli;
using PCL.Frontend.Avalonia.Workflows;

namespace PCL.Core.Backend.Test.App;

[TestClass]
public sealed class FrontendCliWorkflowTest
{
    [TestMethod]
    public void ParseLaunchInstanceCommand_SupportsInstanceOverride()
    {
        var result = AvaloniaCommandParser.Parse([
            "launch-instance",
            "--instance",
            "My Instance"
        ]);

        Assert.IsNull(result.ErrorMessage);
        Assert.IsFalse(result.ShowHelp);
        Assert.IsNotNull(result.Options);
        Assert.AreEqual(AvaloniaCommandKind.LaunchInstance, result.Options.Command);
        Assert.AreEqual("My Instance", result.Options.InstanceNameOverride);
    }

    [TestMethod]
    public void ParseLaunchInstanceCommand_RequiresInstanceValue()
    {
        var result = AvaloniaCommandParser.Parse(["launch-instance"]);

        Assert.IsNull(result.Options);
        Assert.IsFalse(result.ShowHelp);
        Assert.AreEqual("Option '--instance' is required.", result.ErrorMessage);
    }

    [TestMethod]
    public void CreateDefaultOptions_UsesAppCommand()
    {
        var options = AvaloniaCommandParser.CreateDefaultOptions();

        Assert.AreEqual(AvaloniaCommandKind.App, options.Command);
        Assert.AreEqual("modern-fabric", options.Scenario);
        Assert.IsNull(options.InstanceNameOverride);
    }

    [TestMethod]
    public void ParseRegisterCommand_UsesRegisterCommand()
    {
        var result = AvaloniaCommandParser.Parse(["register"]);

        Assert.IsNull(result.ErrorMessage);
        Assert.IsFalse(result.ShowHelp);
        Assert.IsNotNull(result.Options);
        Assert.AreEqual(AvaloniaCommandKind.Register, result.Options.Command);
    }

    [TestMethod]
    public void ParseUnregisterCommand_UsesUnregisterCommand()
    {
        var result = AvaloniaCommandParser.Parse(["unregister"]);

        Assert.IsNull(result.ErrorMessage);
        Assert.IsFalse(result.ShowHelp);
        Assert.IsNotNull(result.Options);
        Assert.AreEqual(AvaloniaCommandKind.Unregister, result.Options.Command);
    }

    [TestMethod]
    public void ParseRegisterCommand_RejectsArguments()
    {
        var result = AvaloniaCommandParser.Parse(["register", "--unexpected"]);

        Assert.IsNull(result.Options);
        Assert.IsFalse(result.ShowHelp);
        Assert.AreEqual("Unexpected argument '--unexpected'.", result.ErrorMessage);
    }

    [TestMethod]
    public void BuildDesktopEntry_WithArguments_EmitsExecAndPath()
    {
        var entry = FrontendLinuxDesktopEntryService.BuildDesktopEntry(
            "PCL-ME Demo",
            ["/opt/PCL-ME/pcl-me", "launch-instance", "--instance", "My Instance"],
            "/opt/PCL-ME/icon.png",
            "/opt/PCL-ME");

        StringAssert.Contains(entry, "Name=PCL-ME Demo");
        StringAssert.Contains(entry, "Exec=/opt/PCL-ME/pcl-me launch-instance --instance \"My Instance\"");
        StringAssert.Contains(entry, "Path=/opt/PCL-ME");
        StringAssert.Contains(entry, "Icon=/opt/PCL-ME/icon.png");
    }

    [TestMethod]
    public void Register_WithMissingExecutable_Fails()
    {
        if (!OperatingSystem.IsLinux())
        {
            Assert.Inconclusive("Desktop entry registration is only supported on Linux.");
        }

        var missingExecutable = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "pcl-me");

        var result = FrontendLinuxDesktopEntryService.Register(missingExecutable);

        Assert.IsFalse(result.IsSuccess);
        StringAssert.Contains(result.Message, "Executable does not exist:");
        StringAssert.Contains(result.Message, missingExecutable);
    }

    [TestMethod]
    public void RegisterAndUnregister_UsesXdgApplicationsDirectory()
    {
        if (!OperatingSystem.IsLinux())
        {
            Assert.Inconclusive("Desktop entry registration is only supported on Linux.");
        }

        var previousDataHome = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
        var tempRoot = Path.Combine(Path.GetTempPath(), "pcl-me-desktop-entry-" + Guid.NewGuid().ToString("N"));
        var executablePath = Path.Combine(tempRoot, "pcl-me");

        try
        {
            Directory.CreateDirectory(tempRoot);
            File.WriteAllText(executablePath, string.Empty);
            Environment.SetEnvironmentVariable("XDG_DATA_HOME", tempRoot);

            var registerResult = FrontendLinuxDesktopEntryService.Register(executablePath);

            Assert.IsTrue(registerResult.IsSuccess);
            Assert.IsNotNull(registerResult.DesktopEntryPath);
            Assert.AreEqual(
                Path.Combine(tempRoot, "applications", "org.pcl.me.frontend.desktop"),
                registerResult.DesktopEntryPath);
            Assert.IsTrue(File.Exists(registerResult.DesktopEntryPath));

            var entry = File.ReadAllText(registerResult.DesktopEntryPath);
            StringAssert.Contains(entry, $"Exec={executablePath}");
            StringAssert.Contains(entry, $"Path={tempRoot}");

            var unregisterResult = FrontendLinuxDesktopEntryService.Unregister();

            Assert.IsTrue(unregisterResult.IsSuccess);
            Assert.IsFalse(File.Exists(registerResult.DesktopEntryPath));
        }
        finally
        {
            Environment.SetEnvironmentVariable("XDG_DATA_HOME", previousDataHome);
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }
}
