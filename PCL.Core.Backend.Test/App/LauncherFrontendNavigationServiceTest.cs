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

        Assert.AreEqual("下载", view.CurrentPageTitle);
        Assert.IsFalse(view.ShowsBackButton);
        Assert.AreEqual(LauncherFrontendPageKind.TopLevel, view.CurrentPage.Kind);
        Assert.AreEqual("下载分区", view.CurrentPage.SidebarGroupTitle);
        Assert.AreEqual("光影包", view.CurrentPage.SidebarItemTitle);
        Assert.AreEqual("光影包", view.SidebarEntries.Single(entry => entry.IsSelected).Title);
        Assert.AreEqual("光影包", LauncherFrontendNavigationService.GetSubpageTitle(view.CurrentRoute));
        CollectionAssert.AreEqual(new[] { "下载", "光影包" }, view.Breadcrumbs.Select(crumb => crumb.Title).ToArray());
        Assert.IsNull(view.BackTarget);
        Assert.IsTrue(view.UtilityEntries.Single(entry => entry.Id == "task-manager").IsVisible);
        Assert.IsFalse(view.UtilityEntries.Single(entry => entry.Id == "game-log").IsVisible);
    }

    [TestMethod]
    public void BuildViewUsesOverrideTitleAndShowsBackButtonForDetailPages()
    {
        var view = LauncherFrontendNavigationService.BuildView(new LauncherFrontendNavigationViewRequest(
            new LauncherFrontendRoute(LauncherFrontendPageKey.InstanceSetup, LauncherFrontendSubpageKey.VersionMod),
            BackstackDepth: 2,
            CurrentPageTitleOverride: "实例设置 - Demo Instance",
            ParentRoute: new LauncherFrontendRoute(LauncherFrontendPageKey.InstanceSelect)));

        Assert.AreEqual("实例设置 - Demo Instance", view.CurrentPageTitle);
        Assert.IsTrue(view.ShowsBackButton);
        Assert.AreEqual(LauncherFrontendPageKind.Secondary, view.CurrentPage.Kind);
        Assert.AreEqual("实例设置分区", view.CurrentPage.SidebarGroupTitle);
        Assert.AreEqual(LauncherFrontendBackTargetKind.Route, view.BackTarget?.Kind);
        Assert.AreEqual(LauncherFrontendPageKey.InstanceSelect, view.BackTarget?.Route?.Page);
        Assert.AreEqual("Mod", view.SidebarEntries.Single(entry => entry.IsSelected).Title);
        CollectionAssert.AreEqual(
            new[] { "实例设置 - Demo Instance", "Mod" },
            view.Breadcrumbs.Select(crumb => crumb.Title).ToArray());
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

        Assert.AreEqual("修改", view.SidebarEntries.Single(entry => entry.IsSelected).Title);
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

        Assert.AreEqual("设置", view.CurrentPageTitle);
        Assert.IsFalse(view.ShowsBackButton);
        Assert.IsNull(view.BackTarget);
        Assert.AreEqual(LauncherFrontendPageKind.TopLevel, view.CurrentPage.Kind);
        Assert.AreEqual("启动", view.SidebarEntries.Single(entry => entry.IsSelected).Title);
        CollectionAssert.AreEqual(new[] { "设置", "启动" }, view.Breadcrumbs.Select(crumb => crumb.Title).ToArray());
    }

    [TestMethod]
    public void BuildViewAttachesCompDetailToDownloadNavigationFamily()
    {
        var view = LauncherFrontendNavigationService.BuildView(new LauncherFrontendNavigationViewRequest(
            new LauncherFrontendRoute(LauncherFrontendPageKey.CompDetail),
            BackstackDepth: 0));

        Assert.AreEqual(LauncherFrontendPageKind.Detail, view.CurrentPage.Kind);
        Assert.AreEqual("下载分区", view.CurrentPage.SidebarGroupTitle);
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
        Assert.AreEqual("世界", view.BackTarget?.Label.Replace("返回到 ", ""));
    }

    [TestMethod]
    public void BuildViewAttachesHelpDetailToToolsNavigationFamily()
    {
        var view = LauncherFrontendNavigationService.BuildView(new LauncherFrontendNavigationViewRequest(
            new LauncherFrontendRoute(LauncherFrontendPageKey.HelpDetail),
            BackstackDepth: 0));

        Assert.AreEqual(LauncherFrontendPageKind.Detail, view.CurrentPage.Kind);
        Assert.AreEqual("工具分区", view.CurrentPage.SidebarGroupTitle);
        Assert.AreEqual(LauncherFrontendBackTargetKind.Route, view.BackTarget?.Kind);
        Assert.AreEqual(LauncherFrontendPageKey.Tools, view.BackTarget?.Route?.Page);
        Assert.IsTrue(view.SidebarEntries.Any());
    }

}
