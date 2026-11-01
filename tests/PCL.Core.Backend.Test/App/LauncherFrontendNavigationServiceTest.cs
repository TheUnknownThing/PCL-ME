using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.App.Essentials;

namespace PCL.Core.Test.App;

[TestClass]
public sealed class LauncherFrontendNavigationServiceTest
{
    [TestMethod]
    public void GetCatalogIncludesTopLevelAndSecondaryPages()
    {
        var catalog = LauncherFrontendNavigationService.GetCatalog();

        CollectionAssert.AreEqual(
            new[]
            {
                LauncherFrontendPageKey.Launch,
                LauncherFrontendPageKey.Download,
                LauncherFrontendPageKey.Setup,
                LauncherFrontendPageKey.Tools
            },
            catalog.TopLevelPages.Select(page => page.Page).ToArray());
        Assert.IsTrue(catalog.SidebarGroups.Any(group => group.Page == LauncherFrontendPageKey.InstanceSetup));
        Assert.IsTrue(catalog.SecondaryPages.Any(page => page.Page == LauncherFrontendPageKey.GameLog));
    }

    [TestMethod]
    public void BuildViewSelectsCurrentPageSidebarAndUtilityEntries()
    {
        var view = LauncherFrontendNavigationService.BuildView(new LauncherFrontendNavigationViewRequest(
            new LauncherFrontendRoute(LauncherFrontendPageKey.Download, LauncherFrontendSubpageKey.DownloadShader),
            BackstackDepth: 0,
            HasRunningTasks: true,
            HasGameLogs: false));

        Assert.IsFalse(view.ShowsBackButton);
        Assert.AreEqual(LauncherFrontendPageKind.TopLevel, view.CurrentPage.Kind);
        Assert.AreEqual(view.CurrentRoute, view.CurrentPage.Route);
        Assert.IsTrue(view.CurrentPage.HasSidebar);
        Assert.AreEqual(LauncherFrontendSubpageKey.DownloadShader, view.SidebarEntries.Single(entry => entry.IsSelected).Route.Subpage);
        Assert.AreEqual(2, view.Breadcrumbs.Count);
        Assert.AreEqual(view.SidebarEntries.Single(entry => entry.IsSelected).Route, view.Breadcrumbs[1].Route);
        Assert.IsNull(view.BackTarget);
        Assert.IsTrue(view.UtilityEntries.Single(entry => entry.Id == "task-manager").IsVisible);
        Assert.IsFalse(view.UtilityEntries.Single(entry => entry.Id == "game-log").IsVisible);
    }

    [TestMethod]
    public void BuildViewUsesProvidedParentRouteForSecondaryPages()
    {
        var view = LauncherFrontendNavigationService.BuildView(new LauncherFrontendNavigationViewRequest(
            new LauncherFrontendRoute(LauncherFrontendPageKey.InstanceSetup, LauncherFrontendSubpageKey.VersionMod),
            BackstackDepth: 2,
            ParentRoute: new LauncherFrontendRoute(LauncherFrontendPageKey.InstanceSelect)));

        Assert.IsTrue(view.ShowsBackButton);
        Assert.AreEqual(LauncherFrontendPageKind.Secondary, view.CurrentPage.Kind);
        Assert.AreEqual(LauncherFrontendBackTargetKind.Route, view.BackTarget?.Kind);
        Assert.AreEqual(LauncherFrontendPageKey.InstanceSelect, view.BackTarget?.Route?.Page);
        Assert.AreEqual(LauncherFrontendSubpageKey.VersionMod, view.SidebarEntries.Single(entry => entry.IsSelected).Route.Subpage);
        Assert.AreEqual(new LauncherFrontendRoute(LauncherFrontendPageKey.InstanceSetup), view.Breadcrumbs[0].Route);
        Assert.AreEqual(view.SidebarEntries.Single(entry => entry.IsSelected).Route, view.Breadcrumbs[1].Route);
    }

    [TestMethod]
    public void BuildViewDoesNotExposeStandaloneDisabledModSidebarEntry()
    {
        var view = LauncherFrontendNavigationService.BuildView(new LauncherFrontendNavigationViewRequest(
            new LauncherFrontendRoute(LauncherFrontendPageKey.InstanceSetup, LauncherFrontendSubpageKey.VersionMod)));

        CollectionAssert.DoesNotContain(
            view.SidebarEntries.Select(entry => entry.Route.Subpage).ToArray(),
            LauncherFrontendSubpageKey.VersionModDisabled);
    }

    [TestMethod]
    public void BuildViewPlacesModifyPageWithCoreInstanceEntries()
    {
        var view = LauncherFrontendNavigationService.BuildView(new LauncherFrontendNavigationViewRequest(
            new LauncherFrontendRoute(LauncherFrontendPageKey.InstanceSetup, LauncherFrontendSubpageKey.VersionInstall)));

        Assert.AreEqual(LauncherFrontendSubpageKey.VersionInstall, view.SidebarEntries.Single(entry => entry.IsSelected).Route.Subpage);
        CollectionAssert.AreEqual(
            new[]
            {
                LauncherFrontendSubpageKey.VersionOverall,
                LauncherFrontendSubpageKey.VersionSetup,
                LauncherFrontendSubpageKey.VersionInstall,
                LauncherFrontendSubpageKey.VersionExport
            },
            view.SidebarEntries
                .Take(4)
                .Select(entry => entry.Route.Subpage)
                .ToArray());
    }

    [TestMethod]
    public void BuildViewKeepsTopLevelNavigationVisibleWhenHistoryExists()
    {
        var view = LauncherFrontendNavigationService.BuildView(new LauncherFrontendNavigationViewRequest(
            new LauncherFrontendRoute(LauncherFrontendPageKey.Setup, LauncherFrontendSubpageKey.SetupLaunch),
            BackstackDepth: 1));

        Assert.IsFalse(view.ShowsBackButton);
        Assert.IsNull(view.BackTarget);
        Assert.AreEqual(LauncherFrontendPageKind.TopLevel, view.CurrentPage.Kind);
        Assert.AreEqual(LauncherFrontendSubpageKey.SetupLaunch, view.SidebarEntries.Single(entry => entry.IsSelected).Route.Subpage);
        Assert.AreEqual(2, view.Breadcrumbs.Count);
    }

    [TestMethod]
    public void BuildViewAttachesCompDetailToDownloadNavigationFamily()
    {
        var view = LauncherFrontendNavigationService.BuildView(new LauncherFrontendNavigationViewRequest(
            new LauncherFrontendRoute(LauncherFrontendPageKey.CompDetail),
            BackstackDepth: 0));

        Assert.AreEqual(LauncherFrontendPageKind.Detail, view.CurrentPage.Kind);
        Assert.IsTrue(view.CurrentPage.HasSidebar);
        Assert.AreEqual(LauncherFrontendBackTargetKind.Route, view.BackTarget?.Kind);
        Assert.AreEqual(LauncherFrontendPageKey.Download, view.BackTarget?.Route?.Page);
        Assert.IsTrue(view.SidebarEntries.Any());
    }

    [TestMethod]
    public void BuildViewUsesProvidedParentRouteForNestedVersionSaves()
    {
        var parentRoute = new LauncherFrontendRoute(
            LauncherFrontendPageKey.InstanceSetup,
            LauncherFrontendSubpageKey.VersionWorld);
        var view = LauncherFrontendNavigationService.BuildView(new LauncherFrontendNavigationViewRequest(
            new LauncherFrontendRoute(LauncherFrontendPageKey.VersionSaves, LauncherFrontendSubpageKey.VersionSavesBackup),
            ParentRoute: parentRoute));

        Assert.AreEqual(LauncherFrontendBackTargetKind.Route, view.BackTarget?.Kind);
        Assert.AreEqual(parentRoute, view.BackTarget?.Route);
    }

    [TestMethod]
    public void BuildViewAttachesHelpDetailToToolsNavigationFamily()
    {
        var view = LauncherFrontendNavigationService.BuildView(new LauncherFrontendNavigationViewRequest(
            new LauncherFrontendRoute(LauncherFrontendPageKey.HelpDetail),
            BackstackDepth: 0));

        Assert.AreEqual(LauncherFrontendPageKind.Detail, view.CurrentPage.Kind);
        Assert.IsTrue(view.CurrentPage.HasSidebar);
        Assert.AreEqual(LauncherFrontendBackTargetKind.Route, view.BackTarget?.Kind);
        Assert.AreEqual(LauncherFrontendPageKey.Tools, view.BackTarget?.Route?.Page);
        Assert.IsTrue(view.SidebarEntries.Any());
    }

}
