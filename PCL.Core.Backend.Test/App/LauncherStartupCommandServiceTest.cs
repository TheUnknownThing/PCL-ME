using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.App.Essentials;

namespace PCL.Core.Test.App;

[TestClass]
public sealed class LauncherStartupCommandServiceTest
{
    [TestMethod]
    public void ParseReturnsNoneWhenArgumentsAreEmpty()
    {
        var result = LauncherStartupCommandService.Parse([]);

        Assert.AreEqual(LauncherStartupCommandKind.None, result.Kind);
        Assert.IsTrue(result.IsValid);
        Assert.IsNull(result.Argument);
    }

    [TestMethod]
    public void ParseReturnsGpuCommandWithTrimmedExecutablePath()
    {
        var result = LauncherStartupCommandService.Parse(["--gpu", "\"C:\\Java\\javaw.exe\""]);

        Assert.AreEqual(LauncherStartupCommandKind.SetGpuPreference, result.Kind);
        Assert.IsTrue(result.IsValid);
        Assert.AreEqual(@"C:\Java\javaw.exe", result.Argument);
    }

    [TestMethod]
    public void ParseMarksGpuCommandInvalidWhenTargetIsMissing()
    {
        var result = LauncherStartupCommandService.Parse(["--gpu"]);

        Assert.AreEqual(LauncherStartupCommandKind.SetGpuPreference, result.Kind);
        Assert.IsFalse(result.IsValid);
        Assert.IsNull(result.Argument);
    }

    [TestMethod]
    public void ParseReturnsNoneForUnsupportedLegacySwitch()
    {
        var result = LauncherStartupCommandService.Parse(["--memory"]);

        Assert.AreEqual(LauncherStartupCommandKind.None, result.Kind);
        Assert.IsTrue(result.IsValid);
    }
}
