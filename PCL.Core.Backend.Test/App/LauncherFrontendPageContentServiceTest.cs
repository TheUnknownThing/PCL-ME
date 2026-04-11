using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.App.Essentials;

namespace PCL.Core.Test.App;

[TestClass]
public sealed class LauncherFrontendPageContentServiceTest
{
    [TestMethod]
    public void BuildLaunchContentIncludesRuntimeFactsAndFiles()
    {
        var content = BuildContent(
            new LauncherFrontendRoute(LauncherFrontendPageKey.Launch),
            includeLaunchData: true);

        Assert.AreEqual("启动页面", content.Eyebrow);
        Assert.AreEqual("Authlib account", content.Facts.Single(fact => fact.Label == "登录").Value);
        Assert.AreEqual("Java 8 (Forge-compatible)", content.Facts.Single(fact => fact.Label == "Java").Value);
        Assert.IsTrue(content.Sections.Any(section =>
            section.Title == "启动信息" &&
            section.Lines.Any(line => line.Contains("legacy-forge", StringComparison.Ordinal))));
        Assert.IsTrue(content.Sections.Any(section =>
            section.Title == "启动前文件" &&
            section.Lines.Any(line => line.Contains("options.txt", StringComparison.Ordinal))));
    }

    [TestMethod]
    public void BuildSetupLaunchContentUsesUserFacingSections()
    {
        var content = BuildContent(
            new LauncherFrontendRoute(LauncherFrontendPageKey.Setup, LauncherFrontendSubpageKey.SetupLaunch),
            includeLaunchData: true);

        Assert.AreEqual("启动页面", content.Eyebrow);
        Assert.AreEqual("启动", content.Facts.Single(fact => fact.Label == "当前分区").Value);
        Assert.IsTrue(content.Sections.Any(section =>
            section.Title == "基础启动参数" &&
            section.Lines.Any(line => line.Contains("版本隔离", StringComparison.Ordinal))));
        Assert.IsTrue(content.Sections.Any(section =>
            section.Title == "高级启动选项" &&
            section.Lines.Any(line => line.Contains("脚本导出", StringComparison.Ordinal))));
    }

    [TestMethod]
    public void BuildDownloadAndToolsPagesUseConcreteActions()
    {
        var installContent = BuildContent(new LauncherFrontendRoute(
            LauncherFrontendPageKey.Download,
            LauncherFrontendSubpageKey.DownloadInstall));
        var helpContent = BuildContent(new LauncherFrontendRoute(
            LauncherFrontendPageKey.Tools,
            LauncherFrontendSubpageKey.ToolsLauncherHelp));
        var testContent = BuildContent(new LauncherFrontendRoute(
            LauncherFrontendPageKey.Tools,
            LauncherFrontendSubpageKey.ToolsTest));

        Assert.AreEqual("自动安装页面", installContent.Eyebrow);
        Assert.IsTrue(installContent.Sections.Any(section =>
            section.Title == "安装流程" &&
            section.Lines.Any(line => line.Contains("Forge", StringComparison.Ordinal))));

        Assert.AreEqual("帮助页面", helpContent.Eyebrow);
        Assert.IsTrue(helpContent.Sections.Any(section =>
            section.Title == "搜索帮助" &&
            section.Lines.Any(line => line.Contains("搜索结果区", StringComparison.Ordinal))));

        Assert.AreEqual("测试页面", testContent.Eyebrow);
        Assert.IsTrue(testContent.Sections.Any(section =>
            section.Title == "下载与皮肤工具" &&
            section.Lines.Any(line => line.Contains("User-Agent", StringComparison.Ordinal))));
        Assert.IsTrue(testContent.Sections.Any(section =>
            section.Title == "服务器工具" &&
            section.Lines.Any(line => line.Contains("服务器地址输入框", StringComparison.Ordinal))));
    }

    [TestMethod]
    public void BuildPageContentDoesNotLeakImplementationTerms()
    {
        var bannedTerms = new[]
        {
            "壳层",
            "前端",
            "后端",
            "合同",
            "迁移",
            "WPF",
            "replacement shell"
        };

        var routes =
            new[]
            {
                new LauncherFrontendRoute(LauncherFrontendPageKey.Launch),
                new LauncherFrontendRoute(LauncherFrontendPageKey.InstanceSelect),
                new LauncherFrontendRoute(LauncherFrontendPageKey.Download, LauncherFrontendSubpageKey.DownloadInstall),
                new LauncherFrontendRoute(LauncherFrontendPageKey.Download, LauncherFrontendSubpageKey.DownloadClient),
                new LauncherFrontendRoute(LauncherFrontendPageKey.Download, LauncherFrontendSubpageKey.DownloadMod),
                new LauncherFrontendRoute(LauncherFrontendPageKey.Download, LauncherFrontendSubpageKey.DownloadForge),
                new LauncherFrontendRoute(LauncherFrontendPageKey.CompDetail),
                new LauncherFrontendRoute(LauncherFrontendPageKey.HomePageMarket),
                new LauncherFrontendRoute(LauncherFrontendPageKey.Setup, LauncherFrontendSubpageKey.SetupLaunch),
                new LauncherFrontendRoute(LauncherFrontendPageKey.Setup, LauncherFrontendSubpageKey.SetupAbout),
                new LauncherFrontendRoute(LauncherFrontendPageKey.Setup, LauncherFrontendSubpageKey.SetupFeedback),
                new LauncherFrontendRoute(LauncherFrontendPageKey.Setup, LauncherFrontendSubpageKey.SetupGameManage),
                new LauncherFrontendRoute(LauncherFrontendPageKey.Setup, LauncherFrontendSubpageKey.SetupJava),
                new LauncherFrontendRoute(LauncherFrontendPageKey.Setup, LauncherFrontendSubpageKey.SetupLauncherMisc),
                new LauncherFrontendRoute(LauncherFrontendPageKey.Setup, LauncherFrontendSubpageKey.SetupLog),
                new LauncherFrontendRoute(LauncherFrontendPageKey.Setup, LauncherFrontendSubpageKey.SetupUI),
                new LauncherFrontendRoute(LauncherFrontendPageKey.Setup, LauncherFrontendSubpageKey.SetupUpdate),
                new LauncherFrontendRoute(LauncherFrontendPageKey.Tools, LauncherFrontendSubpageKey.ToolsLauncherHelp),
                new LauncherFrontendRoute(LauncherFrontendPageKey.Tools, LauncherFrontendSubpageKey.ToolsTest),
                new LauncherFrontendRoute(LauncherFrontendPageKey.HelpDetail),
                new LauncherFrontendRoute(LauncherFrontendPageKey.TaskManager),
                new LauncherFrontendRoute(LauncherFrontendPageKey.GameLog),
                new LauncherFrontendRoute(LauncherFrontendPageKey.InstanceSetup, LauncherFrontendSubpageKey.VersionOverall),
                new LauncherFrontendRoute(LauncherFrontendPageKey.InstanceSetup, LauncherFrontendSubpageKey.VersionSetup),
                new LauncherFrontendRoute(LauncherFrontendPageKey.InstanceSetup, LauncherFrontendSubpageKey.VersionExport),
                new LauncherFrontendRoute(LauncherFrontendPageKey.InstanceSetup, LauncherFrontendSubpageKey.VersionInstall),
                new LauncherFrontendRoute(LauncherFrontendPageKey.InstanceSetup, LauncherFrontendSubpageKey.VersionServer),
                new LauncherFrontendRoute(LauncherFrontendPageKey.InstanceSetup, LauncherFrontendSubpageKey.VersionWorld),
                new LauncherFrontendRoute(LauncherFrontendPageKey.InstanceSetup, LauncherFrontendSubpageKey.VersionScreenshot),
                new LauncherFrontendRoute(LauncherFrontendPageKey.InstanceSetup, LauncherFrontendSubpageKey.VersionMod),
                new LauncherFrontendRoute(LauncherFrontendPageKey.VersionSaves, LauncherFrontendSubpageKey.VersionSavesInfo),
                new LauncherFrontendRoute(LauncherFrontendPageKey.VersionSaves, LauncherFrontendSubpageKey.VersionSavesBackup),
                new LauncherFrontendRoute(LauncherFrontendPageKey.VersionSaves, LauncherFrontendSubpageKey.VersionSavesDatapack)
            };

        foreach (var route in routes)
        {
            var content = BuildContent(route, includeLaunchData: true);
            var text = string.Join(
                Environment.NewLine,
                new[] { content.Eyebrow, content.Summary }
                    .Concat(content.Facts.SelectMany(fact => new[] { fact.Label, fact.Value }))
                    .Concat(content.Sections.Select(section => section.Eyebrow))
                    .Concat(content.Sections.Select(section => section.Title))
                    .Concat(content.Sections.SelectMany(section => section.Lines)));

            foreach (var bannedTerm in bannedTerms)
            {
                Assert.IsFalse(text.Contains(bannedTerm, StringComparison.Ordinal), $"Route {route.Page}/{route.Subpage} still contains banned term: {bannedTerm}");
            }
        }
    }

    private static LauncherFrontendPageContent BuildContent(LauncherFrontendRoute route, bool includeLaunchData = false)
    {
        return LauncherFrontendPageContentService.Build(new LauncherFrontendPageContentRequest(
            LauncherFrontendNavigationService.BuildView(new LauncherFrontendNavigationViewRequest(route)),
            BuildStartupPlan(),
            BuildConsent(),
            BuildPromptLanes(),
            includeLaunchData
                ? new LauncherFrontendLaunchSurfaceData(
                    "legacy-forge",
                    "Authlib account",
                    "DemoPlayer",
                    4,
                    "Java 8 (Forge-compatible)",
                    null,
                    "java-8-runtime",
                    "854 x 480",
                    112,
                    27,
                    "/Users/demo/.minecraft/natives",
                    "/Users/demo/.minecraft/options.txt",
                    true,
                    true,
                    "/Users/demo/exports/Launch.bat",
                    "Legacy Forge Demo launched successfully.")
                : null,
            new LauncherFrontendCrashSurfaceData(
                "PCL-CE-Crash-20260403.zip",
                3,
                true,
                "/Users/demo/.pcl/logs/PCL.log")));
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
