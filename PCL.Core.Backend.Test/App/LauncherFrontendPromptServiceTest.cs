using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.App;
using PCL.Core.App.Essentials;
using PCL.Core.Minecraft;
using PCL.Core.Minecraft.Launch;

namespace PCL.Core.Test.App;

[TestClass]
public sealed class LauncherFrontendPromptServiceTest
{
    [TestMethod]
    public void BuildStartupPromptQueueIncludesEnvironmentWarningBeforeConsentPrompts()
    {
        var startupPlan = new LauncherStartupWorkflowPlan(
            new LauncherStartupImmediateCommandPlan(LauncherStartupImmediateCommandKind.None),
            new LauncherStartupBootstrapResult(Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(), UpdateChannel.Release, "Legacy environment"),
            new LauncherStartupVisualPlan(
                SplashScreen: null,
                TooltipDefaults: new LauncherTooltipPresentationDefaults(300, 400, 9_999_999, LauncherTooltipPlacement.Bottom, 8.0, 4.0)),
            new LauncherStartupPrompt(
                "Legacy environment",
                "环境警告",
                [new LauncherStartupPromptButton("继续", [new LauncherStartupPromptAction(LauncherStartupPromptActionKind.Continue)])],
                IsWarning: true));
        var consent = LauncherStartupConsentService.Evaluate(new LauncherStartupConsentRequest(
            LauncherStartupSpecialBuildKind.Debug,
            IsSpecialBuildHintDisabled: false,
            HasAcceptedEula: false));

        var prompts = LauncherFrontendPromptService.BuildStartupPromptQueue(startupPlan, consent);

        Assert.AreEqual(3, prompts.Count);
        Assert.AreEqual(LauncherFrontendPromptSource.StartupEnvironmentWarning, prompts[0].Source);
        Assert.AreEqual(LauncherFrontendPromptSeverity.Warning, prompts[0].Severity);
        Assert.AreEqual(LauncherFrontendPromptCommandKind.ContinueFlow, prompts[0].Options[0].Commands[0].Kind);
        Assert.AreEqual(LauncherFrontendPromptSource.StartupConsent, prompts[1].Source);
        Assert.AreEqual(LauncherFrontendPromptCommandKind.AcceptConsent, prompts[2].Options[0].Commands[0].Kind);
    }

    [TestMethod]
    public void BuildLaunchPromptQueueMapsLaunchAndJavaPromptCommands()
    {
        var precheck = new MinecraftLaunchPrecheckResult(
            FailureMessage: null,
            [
                new MinecraftLaunchPrompt(
                    "Need to continue",
                    "Precheck",
                    [new MinecraftLaunchPromptButton("继续", [new MinecraftLaunchPromptAction(MinecraftLaunchPromptActionKind.Continue)])])
            ]);
        var javaCompatibilityPrompt = new MinecraftLaunchPrompt(
            "Selected Java is incompatible.",
            "Java Compatibility",
            [
                new MinecraftLaunchPromptButton(
                    "强制启动",
                    [
                        new MinecraftLaunchPromptAction(MinecraftLaunchPromptActionKind.IgnoreJavaCompatibilityOnce),
                        new MinecraftLaunchPromptAction(MinecraftLaunchPromptActionKind.Continue)
                    ]),
                new MinecraftLaunchPromptButton("改用兼容 Java", [new MinecraftLaunchPromptAction(MinecraftLaunchPromptActionKind.Continue)])
            ],
            IsWarning: true);
        var supportPrompt = new MinecraftLaunchPrompt(
            "Support us",
            "Support",
            [new MinecraftLaunchPromptButton("打开", [new MinecraftLaunchPromptAction(MinecraftLaunchPromptActionKind.OpenUrl, "https://example.invalid")])]);
        var javaPrompt = new MinecraftLaunchJavaPrompt(
            "需要 Java",
            "Java",
            [
                new MinecraftLaunchJavaPromptOption("自动下载", MinecraftLaunchJavaPromptDecision.Download),
                new MinecraftLaunchJavaPromptOption("取消", MinecraftLaunchJavaPromptDecision.Abort)
            ],
            DownloadTarget: "java-21");

        var prompts = LauncherFrontendPromptService.BuildLaunchPromptQueue(precheck, supportPrompt, javaCompatibilityPrompt, javaPrompt);

        Assert.AreEqual(4, prompts.Count);
        Assert.AreEqual(LauncherFrontendPromptSource.LaunchPrecheck, prompts[0].Source);
        Assert.AreEqual(LauncherFrontendPromptSource.LaunchJavaCompatibility, prompts[1].Source);
        CollectionAssert.AreEqual(
            new[]
            {
                LauncherFrontendPromptCommandKind.IgnoreJavaCompatibilityOnce,
                LauncherFrontendPromptCommandKind.ContinueFlow
            },
            prompts[1].Options[0].Commands.Select(command => command.Kind).ToArray());
        Assert.AreEqual(LauncherFrontendPromptCommandKind.OpenUrl, prompts[2].Options[0].Commands[0].Kind);
        Assert.AreEqual(LauncherFrontendPromptSource.LaunchJavaDownload, prompts[3].Source);
        Assert.AreEqual("java-21", prompts[3].Options[0].Commands[0].Value);
        Assert.AreEqual(LauncherFrontendPromptCommandKind.AbortLaunch, prompts[3].Options[1].Commands[0].Kind);
    }

    [TestMethod]
    public void BuildCrashPromptQueueMapsCrashActions()
    {
        var prompts = LauncherFrontendPromptService.BuildCrashPromptQueue(new MinecraftCrashOutputPrompt(
            "Crash details",
            "Crash",
            [
                new MinecraftCrashOutputPromptButton("日志", MinecraftCrashOutputPromptActionKind.ViewLog, ClosesPrompt: false),
                new MinecraftCrashOutputPromptButton("导出", MinecraftCrashOutputPromptActionKind.ExportReport)
            ]));

        Assert.AreEqual(1, prompts.Count);
        Assert.IsFalse(prompts[0].Options[0].ClosesPrompt);
        Assert.AreEqual(LauncherFrontendPromptCommandKind.ViewGameLog, prompts[0].Options[0].Commands.Single().Kind);
        Assert.AreEqual(LauncherFrontendPromptCommandKind.ExportCrashReport, prompts[0].Options[1].Commands.Single().Kind);
    }
}
