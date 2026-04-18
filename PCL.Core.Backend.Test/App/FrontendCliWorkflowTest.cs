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
}
