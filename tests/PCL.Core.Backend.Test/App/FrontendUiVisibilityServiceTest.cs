using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.App.Essentials;
using PCL.Frontend.Avalonia.Models;
using PCL.Frontend.Avalonia.Workflows;

namespace PCL.Core.Backend.Test.App;

[TestClass]
public sealed class FrontendUiVisibilityServiceTest
{
    [TestMethod]
    public void FilterNavigationViewRemovesHiddenTopLevelAndSidebarEntries()
    {
        var preferences = CreatePreferences(("UiHiddenPageTools", true), ("UiHiddenSetupJava", true));
        var navigation = CreateNavigation(
            new LauncherFrontendRoute(LauncherFrontendPageKey.Setup, LauncherFrontendSubpageKey.SetupLaunch),
            LauncherFrontendPageKind.TopLevel,
            [
                new LauncherFrontendNavigationEntry("launch", new LauncherFrontendRoute(LauncherFrontendPageKey.Launch), false),
                new LauncherFrontendNavigationEntry("tools", new LauncherFrontendRoute(LauncherFrontendPageKey.Tools), false)
            ],
            [
                new LauncherFrontendNavigationEntry("launch", new LauncherFrontendRoute(LauncherFrontendPageKey.Setup, LauncherFrontendSubpageKey.SetupLaunch), true),
                new LauncherFrontendNavigationEntry("java", new LauncherFrontendRoute(LauncherFrontendPageKey.Setup, LauncherFrontendSubpageKey.SetupJava), false)
            ]);

        var result = FrontendUiVisibilityService.FilterNavigationView(navigation, preferences);

        Assert.AreEqual(1, result.TopLevelEntries.Count);
        Assert.AreEqual(LauncherFrontendPageKey.Launch, result.TopLevelEntries[0].Route.Page);
        Assert.AreEqual(1, result.SidebarEntries.Count);
        Assert.AreEqual(LauncherFrontendSubpageKey.SetupLaunch, result.SidebarEntries[0].Route.Subpage);
    }

    [TestMethod]
    public void NormalizeRouteFallsBackToVisibleSetupRoute()
    {
        var preferences = CreatePreferences(("UiHiddenSetupLaunch", true), ("UiHiddenSetupUi", false));

        var result = FrontendUiVisibilityService.NormalizeRoute(
            new LauncherFrontendRoute(LauncherFrontendPageKey.Setup, LauncherFrontendSubpageKey.SetupLaunch),
            preferences);

        Assert.AreEqual(LauncherFrontendPageKey.Setup, result.Page);
        Assert.AreEqual(LauncherFrontendSubpageKey.SetupUI, result.Subpage);
    }

    [TestMethod]
    public void NormalizeRouteMapsDefaultSetupRouteToLaunchSubpage()
    {
        var preferences = CreatePreferences();

        var result = FrontendUiVisibilityService.NormalizeRoute(
            new LauncherFrontendRoute(LauncherFrontendPageKey.Setup),
            preferences);

        Assert.AreEqual(LauncherFrontendPageKey.Setup, result.Page);
        Assert.AreEqual(LauncherFrontendSubpageKey.SetupLaunch, result.Subpage);
    }

    [TestMethod]
    public void NormalizeRouteMapsDefaultInstanceSetupRouteToSettingsSubpage()
    {
        var preferences = CreatePreferences();

        var result = FrontendUiVisibilityService.NormalizeRoute(
            new LauncherFrontendRoute(LauncherFrontendPageKey.InstanceSetup),
            preferences);

        Assert.AreEqual(LauncherFrontendPageKey.InstanceSetup, result.Page);
        Assert.AreEqual(LauncherFrontendSubpageKey.VersionOverall, result.Subpage);
    }

    [TestMethod]
    public void FilterNavigationViewRemovesDownloadQuiltWhenHideQuiltLoaderIsEnabled()
    {
        var preferences = CreatePreferences(forceShowHiddenItems: false, hideQuiltLoader: true);
        var navigation = CreateNavigation(
            new LauncherFrontendRoute(LauncherFrontendPageKey.Download, LauncherFrontendSubpageKey.DownloadInstall),
            LauncherFrontendPageKind.TopLevel,
            [
                new LauncherFrontendNavigationEntry("download", new LauncherFrontendRoute(LauncherFrontendPageKey.Download), true)
            ],
            [
                new LauncherFrontendNavigationEntry("install", new LauncherFrontendRoute(LauncherFrontendPageKey.Download, LauncherFrontendSubpageKey.DownloadInstall), true),
                new LauncherFrontendNavigationEntry("quilt", new LauncherFrontendRoute(LauncherFrontendPageKey.Download, LauncherFrontendSubpageKey.DownloadQuilt), false)
            ]);

        var result = FrontendUiVisibilityService.FilterNavigationView(navigation, preferences);

        Assert.AreEqual(1, result.SidebarEntries.Count);
        Assert.AreEqual(LauncherFrontendSubpageKey.DownloadInstall, result.SidebarEntries[0].Route.Subpage);
    }

    [TestMethod]
    public void NormalizeRouteFallsBackFromDownloadQuiltWhenHideQuiltLoaderIsEnabled()
    {
        var preferences = CreatePreferences(forceShowHiddenItems: false, hideQuiltLoader: true);

        var result = FrontendUiVisibilityService.NormalizeRoute(
            new LauncherFrontendRoute(LauncherFrontendPageKey.Download, LauncherFrontendSubpageKey.DownloadQuilt),
            preferences);

        Assert.AreEqual(LauncherFrontendPageKey.Download, result.Page);
        Assert.AreEqual(LauncherFrontendSubpageKey.DownloadInstall, result.Subpage);
    }

    [TestMethod]
    public void ForceShowIgnoresHiddenFlags()
    {
        var preferences = CreatePreferences(forceShowHiddenItems: true, hiddenPairs: [("UiHiddenFunctionSelect", true), ("UiHiddenFunctionHidden", true)]);

        Assert.IsTrue(FrontendUiVisibilityService.ShouldShowLaunchInstanceManagement(preferences));
        Assert.IsTrue(FrontendUiVisibilityService.ShouldShowFunctionHiddenCard(preferences));
        Assert.IsTrue(FrontendUiVisibilityService.IsRouteVisible(
            new LauncherFrontendRoute(LauncherFrontendPageKey.Setup, LauncherFrontendSubpageKey.SetupLaunch),
            preferences));
    }

    private static FrontendUiVisibilityPreferences CreatePreferences(
        params (string Key, bool Value)[] hiddenPairs)
    {
        return CreatePreferences(forceShowHiddenItems: false, hideQuiltLoader: false, hiddenPairs);
    }

    private static FrontendUiVisibilityPreferences CreatePreferences(
        bool forceShowHiddenItems,
        bool hideQuiltLoader = false,
        params (string Key, bool Value)[] hiddenPairs)
    {
        var uiState = new FrontendSetupUiState(
            0,
            0,
            0,
            string.Empty,
            string.Empty,
            0,
            FrontendStartupScalingService.DefaultUiScaleFactor,
            true,
            false,
            true,
            0,
            0,
            true,
            1000,
            0,
            0,
            1,
            false,
            string.Empty,
            0,
            string.Empty,
            0,
            [
                new FrontendSetupUiToggleGroup(
                    "测试",
                    hiddenPairs.Select(pair => new FrontendSetupUiToggleItem(pair.Key, pair.Key, pair.Value)).ToArray())
            ]);

        return FrontendUiVisibilityService.BuildPreferences(uiState, forceShowHiddenItems, hideQuiltLoader);
    }

    private static LauncherFrontendNavigationView CreateNavigation(
        LauncherFrontendRoute route,
        LauncherFrontendPageKind kind,
        IReadOnlyList<LauncherFrontendNavigationEntry> topLevelEntries,
        IReadOnlyList<LauncherFrontendNavigationEntry> sidebarEntries)
    {
        return new LauncherFrontendNavigationView(
            route,
            new LauncherFrontendPageSurface(route, kind, sidebarEntries.Count > 0),
            [],
            null,
            false,
            topLevelEntries,
            sidebarEntries,
            []);
    }
}
