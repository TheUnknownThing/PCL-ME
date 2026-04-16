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

        Assert.AreEqual("launch.notifications.success", result.Message.Key);
        Assert.AreEqual("Test Instance", result.Message.Arguments?.Single().StringValue);
        Assert.AreEqual(MinecraftLaunchNotificationKind.Finish, result.Kind);
    }

    [TestMethod]
    public void GetCompletionNotificationPrefersAbortHint()
    {
        var result = MinecraftLaunchShellService.GetCompletionNotification(
            new MinecraftLaunchCompletionRequest("Test Instance", MinecraftLaunchOutcome.Aborted, true, "导出启动脚本成功！"));

        Assert.AreEqual("launch.notifications.abort_with_hint", result.Message.Key);
        Assert.AreEqual("导出启动脚本成功！", result.Message.Arguments?.Single().StringValue);
        Assert.AreEqual(MinecraftLaunchNotificationKind.Finish, result.Kind);
    }

    [TestMethod]
    public void GetCompletionNotificationReturnsDefaultAbortMessage()
    {
        var result = MinecraftLaunchShellService.GetCompletionNotification(
            new MinecraftLaunchCompletionRequest("Test Instance", MinecraftLaunchOutcome.Aborted, false, null));

        Assert.AreEqual("launch.notifications.launch_canceled", result.Message.Key);
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
    public void BuildScriptExportPlanReturnsRevealAndAbortHint()
    {
        var result = MinecraftLaunchShellService.BuildScriptExportPlan(
            "/tmp/Launch.bat");

        Assert.AreEqual("/tmp/Launch.bat", result.TargetPath);
        Assert.AreEqual("导出启动脚本完成，强制结束启动过程", result.CompletionLogMessage);
        Assert.AreEqual("导出启动脚本成功！", result.AbortHint);
        Assert.AreEqual("/tmp/Launch.bat", result.RevealInShellPath);
    }

    [TestMethod]
    public void GetSupportPromptReturnsDonationPromptAtMilestone()
    {
        var result = MinecraftLaunchShellService.GetSupportPrompt(200);

        Assert.IsNotNull(result);
        Assert.AreEqual("launch.prompts.support.title", result.Title.Key);
        Assert.AreEqual("launch.prompts.support.actions.sponsor", result.Buttons[0].Label.Key);
        Assert.AreEqual(MinecraftLaunchPromptActionKind.OpenUrl, result.Buttons[0].Actions[0].Kind);
    }

    [TestMethod]
    public void GetSupportPromptReturnsNullOutsideMilestones()
    {
        Assert.IsNull(MinecraftLaunchShellService.GetSupportPrompt(201));
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

    [TestMethod]
    public void GetPostLaunchShellPlanReturnsMusicVideoVisibilityAndCounterPlan()
    {
        var result = MinecraftLaunchShellService.GetPostLaunchShellPlan(
            new MinecraftLaunchPostLaunchShellRequest(
                LauncherVisibility.HideAndReopen,
                StopMusicInGame: true,
                StartMusicInGame: false));

        Assert.AreEqual(MinecraftLaunchMusicActionKind.Pause, result.MusicAction.Kind);
        Assert.AreEqual("[Music] 已根据设置，在启动后暂停音乐播放", result.MusicAction.LogMessage);
        Assert.AreEqual(MinecraftLaunchVideoBackgroundActionKind.Pause, result.VideoBackgroundAction.Kind);
        Assert.AreEqual(MinecraftLaunchShellActionKind.HideLauncher, result.LauncherAction.Kind);
        Assert.AreEqual(1, result.GlobalLaunchCountIncrement);
        Assert.AreEqual(1, result.InstanceLaunchCountIncrement);
    }

    [TestMethod]
    public void GetWatcherStopShellPlanRestoresLauncherWithoutShutdown()
    {
        var result = MinecraftLaunchShellService.GetWatcherStopShellPlan(
            new MinecraftLaunchWatcherStopShellRequest(
                LauncherVisibility.HideAndExit,
                StopMusicInGame: false,
                StartMusicInGame: true,
                TriggerLauncherShutdown: false));

        Assert.AreEqual(MinecraftLaunchMusicActionKind.Pause, result.MusicAction.Kind);
        Assert.AreEqual("[Music] 已根据设置，在结束后暂停音乐播放", result.MusicAction.LogMessage);
        Assert.AreEqual(MinecraftLaunchVideoBackgroundActionKind.Play, result.VideoBackgroundAction.Kind);
        Assert.AreEqual(MinecraftLaunchShellActionKind.ShowLauncher, result.LauncherAction.Kind);
        Assert.AreEqual(0, result.GlobalLaunchCountIncrement);
        Assert.AreEqual(0, result.InstanceLaunchCountIncrement);
    }

    [TestMethod]
    public void GetWatcherStopShellPlanClosesLauncherWhenHideAndExitShouldShutdown()
    {
        var result = MinecraftLaunchShellService.GetWatcherStopShellPlan(
            new MinecraftLaunchWatcherStopShellRequest(
                LauncherVisibility.HideAndExit,
                StopMusicInGame: true,
                StartMusicInGame: false,
                TriggerLauncherShutdown: true));

        Assert.AreEqual(MinecraftLaunchMusicActionKind.Resume, result.MusicAction.Kind);
        Assert.AreEqual(MinecraftLaunchShellActionKind.ExitLauncher, result.LauncherAction.Kind);
    }
}
