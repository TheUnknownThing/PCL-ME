using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.App.Essentials;

namespace PCL.Core.Test.App;

[TestClass]
public sealed class LauncherFrontendPageContentServiceTest
{
    [TestMethod]
    public void BuildLaunchContentSummarizesRuntimeAndPrerunSeams()
    {
        var content = LauncherFrontendPageContentService.Build(new LauncherFrontendPageContentRequest(
            LauncherFrontendNavigationService.BuildView(new LauncherFrontendNavigationViewRequest(
                new LauncherFrontendRoute(LauncherFrontendPageKey.Launch))),
            BuildStartupPlan(),
            BuildConsent(),
            BuildPromptLanes(),
            new LauncherFrontendLaunchSurfaceData(
                "legacy-forge",
                "Authlib account",
                "DemoPlayer",
                4,
                "Java 8 (Forge-compatible)",
                "java-8-runtime",
                "854 x 480",
                112,
                27,
                "/Users/demo/.minecraft/natives",
                "/Users/demo/.minecraft/options.txt",
                true,
                false,
                null,
                "Legacy Forge Demo launched successfully."),
            new LauncherFrontendCrashSurfaceData(
                "PCL-CE-Crash-20260403.zip",
                3,
                true,
                "/Users/demo/.pcl/logs/PCL.log")));

        Assert.AreEqual("Launch migration surface", content.Eyebrow);
        Assert.AreEqual("Authlib account", content.Facts.Single(fact => fact.Label == "Login").Value);
        Assert.AreEqual("Java 8 (Forge-compatible)", content.Facts.Single(fact => fact.Label == "Java").Value);
        Assert.IsTrue(content.Sections.Any(section =>
            section.Title == "Launch readiness" &&
            section.Lines.Any(line => line.Contains("legacy-forge", StringComparison.Ordinal))));
        Assert.IsTrue(content.Sections.Any(section =>
            section.Title == "Prerun files" &&
            section.Lines.Any(line => line.Contains("options.txt", StringComparison.Ordinal))));
    }

    [TestMethod]
    public void BuildSetupContentUsesStartupAndConsentContracts()
    {
        var content = LauncherFrontendPageContentService.Build(new LauncherFrontendPageContentRequest(
            LauncherFrontendNavigationService.BuildView(new LauncherFrontendNavigationViewRequest(
                new LauncherFrontendRoute(LauncherFrontendPageKey.Setup))),
            BuildStartupPlan(),
            BuildConsent(),
            BuildPromptLanes(),
            new LauncherFrontendLaunchSurfaceData(
                "modern-fabric",
                "Microsoft account",
                "DemoPlayer",
                5,
                "Java 21",
                "java-21-runtime",
                "1280 x 720",
                84,
                21,
                "/Users/demo/.minecraft/natives",
                "/Users/demo/.minecraft/options.txt",
                true,
                true,
                "/Users/demo/exports/Launch.bat",
                "Modern Fabric Demo launched successfully."),
            new LauncherFrontendCrashSurfaceData(
                "PCL-CE-Crash-20260403.zip",
                2,
                true,
                "/Users/demo/.pcl/logs/PCL.log")));

        Assert.AreEqual("Settings migration surface", content.Eyebrow);
        Assert.AreEqual("Release", content.Facts.Single(fact => fact.Label == "Update channel").Value);
        Assert.AreEqual("3", content.Facts.Single(fact => fact.Label == "Consent prompts").Value);
        Assert.IsTrue(content.Sections.Any(section =>
            section.Title == "Startup bootstrap" &&
            section.Lines.Any(line => line.Contains("Directories to create: 2", StringComparison.Ordinal))));
        Assert.IsTrue(content.Sections.Any(section =>
            section.Title == "Java-facing settings seam" &&
            section.Lines.Any(line => line.Contains("Java 21", StringComparison.Ordinal))));
    }

    [TestMethod]
    public void BuildSetupLaunchContentDescribesCopiedLaunchSettingsSurface()
    {
        var content = LauncherFrontendPageContentService.Build(new LauncherFrontendPageContentRequest(
            LauncherFrontendNavigationService.BuildView(new LauncherFrontendNavigationViewRequest(
                new LauncherFrontendRoute(LauncherFrontendPageKey.Setup, LauncherFrontendSubpageKey.SetupLaunch))),
            BuildStartupPlan(),
            BuildConsent(),
            BuildPromptLanes(),
            new LauncherFrontendLaunchSurfaceData(
                "modern-fabric",
                "Microsoft account",
                "DemoPlayer",
                5,
                "Java 21",
                "java-21-runtime",
                "1280 x 720",
                84,
                21,
                "/Users/demo/.minecraft/natives",
                "/Users/demo/.minecraft/options.txt",
                true,
                true,
                "/Users/demo/exports/Launch.bat",
                "Modern Fabric Demo launched successfully.")));

        Assert.AreEqual("启动页面", content.Eyebrow);
        Assert.AreEqual("启动", content.Facts.Single(fact => fact.Label == "当前分区").Value);
        Assert.IsTrue(content.Sections.Any(section =>
            section.Title == "基础启动参数" &&
            section.Lines.Any(line => line.Contains("默认版本隔离", StringComparison.Ordinal))));
    }

    [TestMethod]
    public void BuildSetupAboutContentDescribesCopiedAboutSurface()
    {
        var content = LauncherFrontendPageContentService.Build(new LauncherFrontendPageContentRequest(
            LauncherFrontendNavigationService.BuildView(new LauncherFrontendNavigationViewRequest(
                new LauncherFrontendRoute(LauncherFrontendPageKey.Setup, LauncherFrontendSubpageKey.SetupAbout))),
            BuildStartupPlan(),
            BuildConsent(),
            BuildPromptLanes()));

        Assert.AreEqual("关于页面", content.Eyebrow);
        Assert.AreEqual("关于", content.Facts.Single(fact => fact.Label == "当前分区").Value);
        Assert.IsTrue(content.Sections.Any(section =>
            section.Title == "项目与团队" &&
            section.Lines.Any(line => line.Contains("头像", StringComparison.Ordinal))));
    }

    [TestMethod]
    public void BuildToolsHelpContentDescribesSearchAndListSeams()
    {
        var content = LauncherFrontendPageContentService.Build(new LauncherFrontendPageContentRequest(
            LauncherFrontendNavigationService.BuildView(new LauncherFrontendNavigationViewRequest(
                new LauncherFrontendRoute(LauncherFrontendPageKey.Tools, LauncherFrontendSubpageKey.ToolsLauncherHelp))),
            BuildStartupPlan(),
            BuildConsent(),
            BuildPromptLanes()));

        Assert.AreEqual("帮助页面", content.Eyebrow);
        Assert.AreEqual("帮助", content.Facts.Single(fact => fact.Label == "当前分区").Value);
        Assert.IsTrue(content.Sections.Any(section =>
            section.Title == "搜索帮助" &&
            section.Lines.Any(line => line.Contains("搜索结果区", StringComparison.Ordinal))));
    }

    [TestMethod]
    public void BuildToolsGameLinkContentDescribesLobbyCardsAndTerms()
    {
        var content = LauncherFrontendPageContentService.Build(new LauncherFrontendPageContentRequest(
            LauncherFrontendNavigationService.BuildView(new LauncherFrontendNavigationViewRequest(
                new LauncherFrontendRoute(LauncherFrontendPageKey.Tools, LauncherFrontendSubpageKey.ToolsGameLink))),
            BuildStartupPlan(),
            BuildConsent(),
            BuildPromptLanes()));

        Assert.AreEqual("联机大厅页面", content.Eyebrow);
        Assert.AreEqual("联机大厅", content.Facts.Single(fact => fact.Label == "当前分区").Value);
        Assert.IsTrue(content.Sections.Any(section =>
            section.Title == "加入与创建大厅" &&
            section.Lines.Any(line => line.Contains("加入大厅卡", StringComparison.Ordinal))));
    }

    [TestMethod]
    public void BuildToolsTestContentDescribesUtilityCards()
    {
        var content = LauncherFrontendPageContentService.Build(new LauncherFrontendPageContentRequest(
            LauncherFrontendNavigationService.BuildView(new LauncherFrontendNavigationViewRequest(
                new LauncherFrontendRoute(LauncherFrontendPageKey.Tools, LauncherFrontendSubpageKey.ToolsTest))),
            BuildStartupPlan(),
            BuildConsent(),
            BuildPromptLanes()));

        Assert.AreEqual("测试页面", content.Eyebrow);
        Assert.AreEqual("测试", content.Facts.Single(fact => fact.Label == "当前分区").Value);
        Assert.IsTrue(content.Sections.Any(section =>
            section.Title == "下载与皮肤工具" &&
            section.Lines.Any(line => line.Contains("User-Agent", StringComparison.Ordinal))));
    }

    [TestMethod]
    public void BuildDownloadInstallContentDescribesSelectionStateCards()
    {
        var content = LauncherFrontendPageContentService.Build(new LauncherFrontendPageContentRequest(
            LauncherFrontendNavigationService.BuildView(new LauncherFrontendNavigationViewRequest(
                new LauncherFrontendRoute(LauncherFrontendPageKey.Download, LauncherFrontendSubpageKey.DownloadInstall))),
            BuildStartupPlan(),
            BuildConsent(),
            BuildPromptLanes()));

        Assert.AreEqual("自动安装页面", content.Eyebrow);
        Assert.AreEqual("自动安装", content.Facts.Single(fact => fact.Label == "当前分区").Value);
        Assert.IsTrue(content.Sections.Any(section =>
            section.Title == "安装器选项卡" &&
            section.Lines.Any(line => line.Contains("Forge", StringComparison.Ordinal))));
    }

    private static LauncherStartupWorkflowPlan BuildStartupPlan()
    {
        return LauncherStartupWorkflowService.BuildPlan(new LauncherStartupWorkflowRequest(
            CommandLineArguments: ["--memory"],
            ExecutableDirectory: @"C:\PCL\",
            TempDirectory: @"C:\PCL\Temp\",
            AppDataDirectory: @"C:\Users\demo\AppData\Roaming\PCL\",
            IsBetaVersion: false,
            DetectedWindowsVersion: new Version(10, 0, 19045),
            Is64BitOperatingSystem: true,
            ShowStartupLogo: true));
    }

    private static LauncherStartupConsentResult BuildConsent()
    {
        return LauncherStartupConsentService.Evaluate(new LauncherStartupConsentRequest(
            LauncherStartupSpecialBuildKind.Ci,
            IsSpecialBuildHintDisabled: false,
            HasAcceptedEula: false,
            IsTelemetryDefault: true));
    }

    private static LauncherFrontendPromptLaneSummary[] BuildPromptLanes()
    {
        return
        [
            new LauncherFrontendPromptLaneSummary("startup", "Startup", "Consent prompts", 3, false),
            new LauncherFrontendPromptLaneSummary("launch", "Launch", "Launch prompts", 2, true),
            new LauncherFrontendPromptLaneSummary("crash", "Crash", "Crash prompts", 1, false)
        ];
    }
}
