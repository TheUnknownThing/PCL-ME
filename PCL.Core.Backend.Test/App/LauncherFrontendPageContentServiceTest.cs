using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.App.Essentials;
using PCL.Core.Testing;
using PCL.Frontend.Avalonia.Workflows;

namespace PCL.Core.Test.App;

[TestClass]
public sealed class LauncherFrontendPageContentServiceTest
{
    [TestMethod]
    public void BuildPageContentIncludesLaunchFactsAndLines()
    {
        var content = BuildContent(
            new LauncherFrontendRoute(LauncherFrontendPageKey.Launch),
            CreateI18n(),
            includeLaunchData: true);

        Assert.AreEqual("Top-level page", content.Eyebrow);
        Assert.AreEqual("Launch summary", content.Summary);
        Assert.AreEqual("DemoPlayer", content.Facts.Single(fact => fact.Label == "Identity").Value);
        Assert.AreEqual("Java 8 (Forge-compatible)", content.Facts.Single(fact => fact.Label == "Java").Value);
        Assert.IsTrue(content.Sections.Single().Lines.Any(line =>
            line.Contains("Runtime Java 8 (Forge-compatible) at 854 x 480", StringComparison.Ordinal)));
        Assert.IsTrue(content.Sections.Single().Lines.Any(line =>
            line.Contains("Legacy Forge Demo launched successfully.", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void BuildPageContentUsesRouteSummaryAndBackTargetFacts()
    {
        var content = BuildContent(
            new LauncherFrontendRoute(LauncherFrontendPageKey.InstanceSetup, LauncherFrontendSubpageKey.VersionMod),
            CreateI18n(),
            parentRoute: new LauncherFrontendRoute(LauncherFrontendPageKey.InstanceSelect));

        Assert.AreEqual("Secondary page", content.Eyebrow);
        Assert.AreEqual("Manage installed mods.", content.Summary);
        Assert.AreEqual("Instance settings", content.Facts.Single(fact => fact.Label == "Current page").Value);
        Assert.AreEqual("Mods", content.Facts.Single(fact => fact.Label == "Current section").Value);
        Assert.AreEqual("Back to Instance selection", content.Facts.Single(fact => fact.Label == "Back target").Value);
    }

    [TestMethod]
    public void BuildPageContentIncludesCrashFactsForGameLog()
    {
        var content = BuildContent(
            new LauncherFrontendRoute(LauncherFrontendPageKey.GameLog),
            CreateI18n(),
            includeCrashData: true);

        Assert.AreEqual("Utility page", content.Eyebrow);
        Assert.AreEqual("Crash log and export history.", content.Summary);
        Assert.AreEqual("PCL-ME-Crash-20260403.zip", content.Facts.Single(fact => fact.Label == "Archive").Value);
        Assert.AreEqual("3", content.Facts.Single(fact => fact.Label == "Source files").Value);
        Assert.IsTrue(content.Sections.Single().Lines.Any(line =>
            line.Contains("Export PCL-ME-Crash-20260403.zip with 3 files", StringComparison.Ordinal)));
    }

    private static LauncherFrontendPageContent BuildContent(
        LauncherFrontendRoute route,
        DictionaryI18nService i18n,
        bool includeLaunchData = false,
        bool includeCrashData = false,
        LauncherFrontendRoute? parentRoute = null)
    {
        var navigation = LauncherFrontendNavigationService.BuildView(new LauncherFrontendNavigationViewRequest(
            route,
            ParentRoute: parentRoute));
        var shellPlan = new LauncherFrontendShellPlan(
            BuildStartupPlan(),
            BuildConsent(),
            [],
            LauncherFrontendNavigationService.GetCatalog(),
            navigation);

        return FrontendShellLocalizationService.BuildPageContent(
            shellPlan,
            navigation,
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
            includeCrashData
                ? new LauncherFrontendCrashSurfaceData(
                    "PCL-ME-Crash-20260403.zip",
                    3,
                    true,
                    "/Users/demo/.pcl/logs/PCL.log")
                : null,
            i18n);
    }

    private static LauncherStartupWorkflowPlan BuildStartupPlan()
    {
        return LauncherStartupWorkflowService.BuildPlan(new LauncherStartupWorkflowRequest(
            CommandLineArguments: [],
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
            HasAcceptedEula: false));
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

    private static DictionaryI18nService CreateI18n()
    {
        return new DictionaryI18nService(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["shell.navigation.pages.launch.title"] = "Launch",
            ["shell.navigation.pages.launch.summary"] = "Launch summary",
            ["shell.navigation.pages.game_log.title"] = "Game Log",
            ["shell.navigation.pages.game_log.summary"] = "Crash log and export history.",
            ["shell.navigation.pages.instance_setup.title"] = "Instance settings",
            ["shell.navigation.pages.instance_setup.summary"] = "Manage this instance.",
            ["shell.navigation.pages.instance_select.title"] = "Instance selection",
            ["shell.navigation.pages.instance_select.summary"] = "Choose an instance to launch.",
            ["shell.navigation.subpages.version_mod.title"] = "Mods",
            ["shell.navigation.subpages.version_mod.summary"] = "Manage installed mods.",
            ["shell.page_content.facts.current_page"] = "Current page",
            ["shell.page_content.facts.current_section"] = "Current section",
            ["shell.page_content.facts.prompt_count"] = "Prompt count",
            ["shell.page_content.facts.active_prompt_lane"] = "Active prompt lane",
            ["shell.page_content.facts.page_kind"] = "Page kind",
            ["shell.page_content.facts.back_target"] = "Back target",
            ["shell.page_content.facts.launch.identity"] = "Identity",
            ["shell.page_content.facts.launch.java"] = "Java",
            ["shell.page_content.facts.launch.resolution"] = "Resolution",
            ["shell.page_content.facts.launch.classpath_count"] = "Classpath entries",
            ["shell.page_content.facts.crash.archive_name"] = "Archive",
            ["shell.page_content.facts.crash.source_file_count"] = "Source files",
            ["shell.page_content.values.none"] = "None",
            ["shell.page_content.lines.active_prompt_lane"] = "Active prompt lane: {lane}",
            ["shell.page_content.lines.launch.runtime"] = "Runtime {java} at {resolution}",
            ["shell.page_content.lines.launch.last_completion"] = "Last launch: {message}",
            ["shell.page_content.lines.crash.export"] = "Export {archive_name} with {file_count} files",
            ["shell.page_content.sections.overview.eyebrow"] = "Overview",
            ["shell.page_content.eyebrows.top_level"] = "Top-level page",
            ["shell.page_content.eyebrows.secondary"] = "Secondary page",
            ["shell.page_content.eyebrows.detail"] = "Detail page",
            ["shell.page_content.eyebrows.utility"] = "Utility page",
            ["shell.navigation.utilities.back_target"] = "Back to {target}"
        });
    }
}
