using System.Reflection;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Frontend.Avalonia.Desktop.Panes.Right;
using PCL.Frontend.Avalonia.ViewModels;
using static PCL.Frontend.Avalonia.E2E.DesktopFixture;

namespace PCL.Frontend.Avalonia.E2E;

[TestClass]
[DoNotParallelize]
public sealed class DownloadCatalogE2E
{
    private const double BoundedChoiceListMaxHeight = 420;

    [TestMethod]
    public Task InstallerCatalog_LongVersionListScrollsInsideABoundedCard() => Run(async () =>
    {
        await using var app = new DesktopFixture();
        await app.Start();
        await app.Session.NavigateAsync("Download/DownloadForge");
        await Wait(() => app.Window.GetVisualDescendants().OfType<DownloadCatalogRightPaneView>().Any(), "The installer catalog did not open.");
        var view = app.Window.GetVisualDescendants().OfType<DownloadCatalogRightPaneView>().Single();
        SeedForgeVersionSection(app.Launcher);
        PclCard? SectionCard() => view.GetVisualDescendants().OfType<PclCard>()
            .SingleOrDefault(section => section.DataContext is DownloadCatalogSectionViewModel);
        await Wait(() => SectionCard() is { IsEffectivelyVisible: true }, "The seeded installer section did not appear.");
        var card = SectionCard()!;
        await Click(app.Window, card.Find<Button>("HeaderButton")!);
        await Wait(() => card.DataContext is DownloadCatalogSectionViewModel { IsExpanded: true } &&
                       card.GetVisualDescendants().OfType<PclListItem>().Any(), "Catalog versions did not appear.");
        await Settle();

        var scroller = card.GetVisualDescendants().OfType<ScrollViewer>()
            .Single(scroll => scroll.Content is ItemsControl { ItemCount: 120 });
        Assert.AreEqual(BoundedChoiceListMaxHeight, scroller.MaxHeight, 0.01);
        Assert.IsTrue(scroller.Extent.Height > scroller.Viewport.Height * 4,
            $"The version list must scroll internally instead of stretching the card (extent {scroller.Extent.Height}, viewport {scroller.Viewport.Height}).");
        Assert.IsTrue(scroller.Viewport.Height <= BoundedChoiceListMaxHeight + 0.5,
            $"The version list viewport must stay bounded (was {scroller.Viewport.Height}).");
        Assert.IsTrue(card.Bounds.Height < 600,
            $"An expanded installer section must not fill the page (height {card.Bounds.Height}).");
        Assert.IsTrue(scroller.GetVisualDescendants().OfType<PclListItem>().Count() < 30,
            "Long version lists must remain virtualized inside the bounded card.");
        app.Screenshot("download-catalog-forge-expanded");

        scroller.ScrollToEnd();
        await Settle();
        var lastVersion = scroller.GetVisualDescendants().OfType<PclListItem>().Single(item => item.Title == "1.20.1 - 119");
        await Click(app.Window, lastVersion);

        await Click(app.Window, card.Find<Button>("HeaderButton")!);
        await Wait(() => card.Bounds.Height <= 45, "The installer section did not collapse back to its header height.");
        app.Screenshot("download-catalog-forge-collapsed");
    });

    private static void SeedForgeVersionSection(LauncherViewModel launcher)
    {
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        // Invalidate the in-flight network refresh so it cannot replace the synthetic section.
        var version = typeof(LauncherViewModel).GetField("_downloadCatalogRefreshVersion", flags)!;
        version.SetValue(launcher, (int)version.GetValue(launcher)! + 1);
        typeof(LauncherViewModel).GetMethod("SetDownloadCatalogLoading", flags)!
            .Invoke(launcher, [false]);
        var command = new ActionCommand(() => { });
        launcher.DownloadCatalogSections.Add(new DownloadCatalogSectionViewModel(
            "1.20.1 (120)",
            Enumerable.Range(0, 120).Select(index => new DownloadCatalogEntryViewModel(
                $"1.20.1 - {index}",
                $"47.2.{index}",
                string.Empty,
                "save_installer",
                command)).ToArray(),
            isCollapsible: true,
            isExpanded: false));
    }
}
