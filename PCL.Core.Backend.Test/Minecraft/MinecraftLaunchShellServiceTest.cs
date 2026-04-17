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
            new MinecraftLaunchCompletionRequest("Test Instance", MinecraftLaunchOutcome.Aborted, true, "Launch script exported successfully!"));

        Assert.AreEqual("launch.notifications.abort_with_hint", result.Message.Key);
        Assert.AreEqual("Launch script exported successfully!", result.Message.Arguments?.Single().StringValue);
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

        Assert.AreEqual("Failed to export launch script", result.DialogTitle);
        Assert.AreEqual("Failed to export launch script", result.LogTitle);
    }

    [TestMethod]
    public void BuildScriptExportPlanReturnsRevealAndAbortHint()
    {
        var result = MinecraftLaunchShellService.BuildScriptExportPlan(
            "/tmp/Launch.bat");

        Assert.AreEqual("/tmp/Launch.bat", result.TargetPath);
        Assert.AreEqual("Launch script export completed; forcing the launch process to end", result.CompletionLogMessage);
        Assert.AreEqual("Launch script exported successfully!", result.AbortHint);
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
    public void GetPostLaunchShellPlanReturnsVideoVisibilityAndCounterPlan()
    {
        var result = MinecraftLaunchShellService.GetPostLaunchShellPlan(
            new MinecraftLaunchPostLaunchShellRequest(
                LauncherVisibility.HideAndReopen));

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
                TriggerLauncherShutdown: false));

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
                TriggerLauncherShutdown: true));

        Assert.AreEqual(MinecraftLaunchShellActionKind.ExitLauncher, result.LauncherAction.Kind);
    }
}
