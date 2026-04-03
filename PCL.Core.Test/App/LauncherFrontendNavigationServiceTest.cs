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
        Assert.AreEqual("光影包", view.SidebarEntries.Single(entry => entry.IsSelected).Title);
        Assert.AreEqual("光影包", LauncherFrontendNavigationService.GetSubpageTitle(view.CurrentRoute));
        Assert.IsTrue(view.UtilityEntries.Single(entry => entry.Id == "task-manager").IsVisible);
        Assert.IsFalse(view.UtilityEntries.Single(entry => entry.Id == "game-log").IsVisible);
    }

    [TestMethod]
    public void BuildViewUsesOverrideTitleAndShowsBackButtonForDetailPages()
    {
        var view = LauncherFrontendNavigationService.BuildView(new LauncherFrontendNavigationViewRequest(
            new LauncherFrontendRoute(LauncherFrontendPageKey.InstanceSetup, LauncherFrontendSubpageKey.VersionMod),
            BackstackDepth: 1,
            CurrentPageTitleOverride: "实例设置 - Demo Instance"));

        Assert.AreEqual("实例设置 - Demo Instance", view.CurrentPageTitle);
        Assert.IsTrue(view.ShowsBackButton);
        Assert.AreEqual("Mod", view.SidebarEntries.Single(entry => entry.IsSelected).Title);
    }
}
