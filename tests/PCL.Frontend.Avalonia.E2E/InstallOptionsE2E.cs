using System.Reflection;
using System.Text.Json.Nodes;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Frontend.Avalonia.Desktop.Controls;
using PCL.Frontend.Avalonia.Desktop.Panes.Right;
using PCL.Frontend.Avalonia.ViewModels;
using PCL.Frontend.Avalonia.Workflows;
using PCL.UI.Controls;
using static PCL.Frontend.Avalonia.E2E.DesktopFixture;

namespace PCL.Frontend.Avalonia.E2E;

[TestClass]
[DoNotParallelize]
public sealed class InstallOptionsE2E
{
    private const double BoundedChoiceListMaxHeight = 420;

    [TestMethod]
    public Task OptionCard_LongChoiceListScrollsInsideABoundedCard() => Run(async () =>
    {
        await using var app = new DesktopFixture();
        await app.Start();
        SeedCatalog(app.Launcher);
        await Click(app.Window, app.Window.GetVisualDescendants().OfType<PclTabButton>()
            .Single(tab => tab.IsEffectivelyVisible && tab.Text == "Download"));
        await Wait(() => app.Window.GetVisualDescendants().OfType<DownloadInstallRightPaneView>().Any(), "Version catalog did not open.");
        var view = app.Window.GetVisualDescendants().OfType<DownloadInstallRightPaneView>().Single();
        var field = view.GetVisualDescendants().OfType<PclSearchBox>().Single().Find<TextBox>("SearchTextBox")!;
        await Type(app.Window, field, "1.21.119");
        var list = view.Find<ItemsControl>("MinecraftCatalogExpandedList")!;
        await Wait(() => view.Find<PclCard>("MinecraftCatalogExpandedHost")!.IsVisible && list.ItemCount == 1, "Exact version search did not update.");
        await Settle();
        await Click(app.Window, list.GetVisualDescendants().OfType<PclListItem>().Single(item => item.Title == "1.21.119"));
        await Wait(() => app.Launcher.ShowDownloadInstallSelectionStage, "Selecting a version did not open installation options.");

        SeedForgeChoices(app.Launcher, 120);

        PclCard ForgeCard() => view.GetVisualDescendants().OfType<PclCard>()
            .Single(card => card.DataContext is DownloadInstallOptionCardViewModel { Title: "Forge" });
        await Click(app.Window, ForgeCard().Find<Button>("HeaderButton")!);
        var card = ForgeCard();
        await Wait(() => card.DataContext is DownloadInstallOptionCardViewModel { IsExpanded: true } &&
                       card.GetVisualDescendants().OfType<PclListItem>().Any(), "Forge choices did not appear.");
        await Settle();

        var scroller = card.GetVisualDescendants().OfType<ScrollViewer>()
            .Single(scroll => scroll.Content is ItemsControl { ItemCount: 120 });
        Assert.AreEqual(BoundedChoiceListMaxHeight, scroller.MaxHeight, 0.01);
        Assert.IsTrue(scroller.Extent.Height > scroller.Viewport.Height * 4,
            $"The choice list must scroll internally instead of stretching the card (extent {scroller.Extent.Height}, viewport {scroller.Viewport.Height}).");
        Assert.IsTrue(scroller.Viewport.Height <= BoundedChoiceListMaxHeight + 0.5,
            $"The choice list viewport must stay bounded (was {scroller.Viewport.Height}).");
        Assert.IsTrue(card.Bounds.Height < 600,
            $"An expanded option card must not fill the page (height {card.Bounds.Height}).");
        Assert.IsTrue(scroller.GetVisualDescendants().OfType<PclListItem>().Count() < 30,
            "Long choice lists must remain virtualized inside the bounded card.");
        app.Screenshot("install-forge-expanded");

        scroller.ScrollToEnd();
        await Settle();
        var lastChoice = scroller.GetVisualDescendants().OfType<PclListItem>().Single(item => item.Title == "Forge 119");
        await Click(app.Window, lastChoice);
        await Wait(() => ForgeCard().Bounds.Height <= 45, "Selecting a choice did not collapse the card back to its header height.");
        Assert.AreEqual("Forge 119", ((DownloadInstallOptionCardViewModel)ForgeCard().DataContext!).SelectionText);
        app.Screenshot("install-forge-selected");
    });

    private static void SeedCatalog(LauncherViewModel launcher)
    {
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var version = typeof(LauncherViewModel).GetField("_downloadInstallMinecraftCatalogVersion", flags)!;
        version.SetValue(launcher, (int)version.GetValue(launcher)! + 1);
        Set("_downloadInstallMinecraftCatalogLoaded", true);
        Set("_isDownloadInstallMinecraftCatalogLoading", false);
        Set("_downloadInstallMinecraftCatalogChoices", new[]
        {
            Choice("1.21.119", "release")
        });

        void Set(string name, object value) => typeof(LauncherViewModel).GetField(name, flags)!.SetValue(launcher, value);
        static FrontendInstallChoice Choice(string id, string group) => new(id, id, id, id, FrontendInstallChoiceKind.Minecraft,
            Metadata: new JsonObject { ["catalogGroup"] = group });
    }

    private static void SeedForgeChoices(LauncherViewModel launcher, int count)
    {
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var cache = (Dictionary<string, IReadOnlyList<FrontendInstallChoice>>)typeof(LauncherViewModel)
            .GetField("_downloadInstallOptionChoices", flags)!.GetValue(launcher)!;
        cache["Forge"] = Enumerable.Range(0, count)
            .Select(index => new FrontendInstallChoice(
                $"forge-{index}",
                $"Forge {index}",
                $"{index}.0.{index}",
                "1.21.119",
                FrontendInstallChoiceKind.Forge))
            .ToArray();
    }
}
