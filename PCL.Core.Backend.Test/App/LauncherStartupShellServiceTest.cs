using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.App.Essentials;

namespace PCL.Core.Test.App;

[TestClass]
public sealed class LauncherStartupShellServiceTest
{
    [TestMethod]
    public void ResolveImmediateCommandReturnsNoneWhenArgumentsAreEmpty()
    {
        var result = LauncherStartupShellService.ResolveImmediateCommand([]);

        Assert.AreEqual(LauncherStartupImmediateCommandKind.None, result.Kind);
        Assert.IsNull(result.Argument);
        Assert.IsNull(result.InvalidMessage);
    }

    [TestMethod]
    public void ResolveImmediateCommandReturnsGpuPreferencePlanForValidGpuCommand()
    {
        var result = LauncherStartupShellService.ResolveImmediateCommand(["--gpu", "\"C:\\Java\\javaw.exe\""]);

        Assert.AreEqual(LauncherStartupImmediateCommandKind.SetGpuPreference, result.Kind);
        Assert.AreEqual(@"C:\Java\javaw.exe", result.Argument);
        Assert.IsNull(result.InvalidMessage);
    }

    [TestMethod]
    public void ResolveImmediateCommandReturnsInvalidPlanWhenGpuTargetIsMissing()
    {
        var result = LauncherStartupShellService.ResolveImmediateCommand(["--gpu"]);

        Assert.AreEqual(LauncherStartupImmediateCommandKind.Invalid, result.Kind);
        Assert.IsNull(result.Argument);
        Assert.AreEqual("缺少需要设置显卡偏好的可执行文件路径。", result.InvalidMessage);
    }

    [TestMethod]
    public void ResolveImmediateCommandReturnsMemoryPlanForMemorySwitch()
    {
        var result = LauncherStartupShellService.ResolveImmediateCommand(["--memory"]);

        Assert.AreEqual(LauncherStartupImmediateCommandKind.OptimizeMemory, result.Kind);
        Assert.IsNull(result.Argument);
        Assert.IsNull(result.InvalidMessage);
    }

    [TestMethod]
    public void GetEnvironmentWarningPromptReturnsWarningPromptWhenMessageExists()
    {
        var result = LauncherStartupShellService.GetEnvironmentWarningPrompt("warning");

        Assert.IsNotNull(result);
        Assert.AreEqual("环境警告", result.Title);
        Assert.IsTrue(result.IsWarning);
        Assert.AreEqual(1, result.Buttons.Count);
        Assert.AreEqual("我知道了", result.Buttons[0].Label);
        Assert.AreEqual(LauncherStartupPromptActionKind.Continue, result.Buttons[0].Actions[0].Kind);
    }

    [TestMethod]
    public void GetEnvironmentWarningPromptReturnsNullForEmptyMessage()
    {
        Assert.IsNull(LauncherStartupShellService.GetEnvironmentWarningPrompt(""));
        Assert.IsNull(LauncherStartupShellService.GetEnvironmentWarningPrompt(" "));
        Assert.IsNull(LauncherStartupShellService.GetEnvironmentWarningPrompt(null));
    }
}
