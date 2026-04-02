using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.App;
using PCL.Core.Minecraft.Launch;

namespace PCL.Core.Test.Minecraft;

[TestClass]
public sealed class MinecraftLaunchShellServiceTest
{
    [TestMethod]
    public void GetCompletionNotificationReturnsSuccessMessage()
    {
        var result = MinecraftLaunchShellService.GetCompletionNotification(
            new MinecraftLaunchCompletionRequest("Test Instance", MinecraftLaunchOutcome.Succeeded, false, null));

        Assert.AreEqual("Test Instance 启动成功！", result.Message);
        Assert.AreEqual(MinecraftLaunchNotificationKind.Finish, result.Kind);
    }

    [TestMethod]
    public void GetCompletionNotificationPrefersAbortHint()
    {
        var result = MinecraftLaunchShellService.GetCompletionNotification(
            new MinecraftLaunchCompletionRequest("Test Instance", MinecraftLaunchOutcome.Aborted, true, "导出启动脚本成功！"));

        Assert.AreEqual("导出启动脚本成功！", result.Message);
        Assert.AreEqual(MinecraftLaunchNotificationKind.Finish, result.Kind);
    }

    [TestMethod]
    public void GetCompletionNotificationReturnsDefaultAbortMessage()
    {
        var result = MinecraftLaunchShellService.GetCompletionNotification(
            new MinecraftLaunchCompletionRequest("Test Instance", MinecraftLaunchOutcome.Aborted, false, null));

        Assert.AreEqual("已取消启动！", result.Message);
        Assert.AreEqual(MinecraftLaunchNotificationKind.Info, result.Kind);
    }

    [TestMethod]
    public void GetFailureDisplayReturnsScriptExportTitles()
    {
        var result = MinecraftLaunchShellService.GetFailureDisplay(isScriptExport: true);

        Assert.AreEqual("导出启动脚本失败", result.DialogTitle);
        Assert.AreEqual("导出启动脚本失败", result.LogTitle);
    }

    [TestMethod]
    public void GetPostLaunchShellActionMapsVisibilityModes()
    {
        Assert.AreEqual(
            MinecraftLaunchShellActionKind.ExitLauncher,
            MinecraftLaunchShellService.GetPostLaunchShellAction(LauncherVisibility.ExitImmediately).Kind);
        Assert.AreEqual(
            MinecraftLaunchShellActionKind.HideLauncher,
            MinecraftLaunchShellService.GetPostLaunchShellAction(LauncherVisibility.HideAndReopen).Kind);
        Assert.AreEqual(
            MinecraftLaunchShellActionKind.MinimizeLauncher,
            MinecraftLaunchShellService.GetPostLaunchShellAction(LauncherVisibility.MinimizeAndReopen).Kind);
        Assert.AreEqual(
            MinecraftLaunchShellActionKind.None,
            MinecraftLaunchShellService.GetPostLaunchShellAction(LauncherVisibility.DoNothing).Kind);
    }
}
