using System.Reflection;
using System.Text.Json.Nodes;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Frontend.Avalonia.ViewModels;
using PCL.Frontend.Avalonia.Desktop.Controls;
using PCL.Frontend.Avalonia.Desktop.Panes.Right;
using PCL.Frontend.Avalonia.Workflows;
using static PCL.Frontend.Avalonia.Test.FrontendProfileEditingTest;
using static PCL.Frontend.Avalonia.Test.FrontendDeferredContentTest;

namespace PCL.Frontend.Avalonia.Test;

[TestClass]
[DoNotParallelize]
public sealed class FrontendVersionSearchTest
{
    [TestMethod]
    public async Task AutomaticCatalog_SearchesAllGroupsAndClearsWithoutChangingTheSelection()
    {
        await FrontendMotionTest.RunAsync(() =>
        {
            using var fixture = new ProfileFixture();
            var launcher = fixture.Launcher;
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            void Set(string name, object value) => typeof(LauncherViewModel).GetField(name, flags)!.SetValue(launcher, value);
            Set("_downloadInstallMinecraftCatalogVersion", (int)GetField(launcher, "_downloadInstallMinecraftCatalogVersion")! + 1);
            Set("_downloadInstallMinecraftCatalogLoaded", true);
            Set("_isDownloadInstallMinecraftCatalogLoading", false);
            FrontendInstallChoice[] choices =
            [
                CreateChoice("1.21", "release"),
                CreateChoice("1.8.9", "release"),
                CreateChoice("24w14a", "preview"),
                CreateChoice("b1.8.1", "legacy")
            ];
            Set("_downloadInstallMinecraftCatalogChoices", choices);
            var selection = GetField(launcher, "_downloadInstallMinecraftChoice");
            launcher.DownloadInstallMinecraftSearchQuery = " 1.8.9 ";
            Assert.AreEqual(1, launcher.DownloadInstallMinecraftSections.Count);
            Assert.AreEqual("1.8.9", launcher.DownloadInstallMinecraftSections[0].Choices.Single().Title);
            Assert.IsTrue(launcher.DownloadInstallMinecraftSections[0].IsExpanded);
            Assert.IsFalse(launcher.DownloadInstallMinecraftSections[0].CanCollapse);
            Assert.IsNull(launcher.DownloadInstallMinecraftSections[0].HeaderCommand);

            launcher.DownloadInstallMinecraftSearchQuery = "24W14A";
            Assert.AreEqual("24w14a", launcher.DownloadInstallMinecraftSections.Single().Choices.Single().Title);
            launcher.DownloadInstallMinecraftSearchQuery = "missing";
            Assert.IsTrue(launcher.ShowDownloadInstallMinecraftCatalogEmptyState);
            Assert.AreEqual(launcher.DownloadVersionSearchEmptyText, launcher.DownloadInstallMinecraftCatalogStatus);

            launcher.DownloadInstallMinecraftSearchQuery = string.Empty;
            Assert.IsTrue(launcher.DownloadInstallMinecraftSections.Count > 1);
            Assert.IsTrue(launcher.DownloadInstallMinecraftSections.SelectMany(section => section.Choices).Any(choice => choice.Title == "b1.8.1"));
            Assert.AreSame(selection, GetField(launcher, "_downloadInstallMinecraftChoice"));
            return Task.CompletedTask;
        });
    }

    [TestMethod]
    public void ManualCatalog_FiltersEntriesAndRestoresTheCollapsedState()
    {
        var first = CreateEntry("1.8.9", "Release");
        var snapshot = CreateEntry("24w14a", "Snapshot");
        var section = new DownloadCatalogSectionViewModel("Minecraft", [first, snapshot], isCollapsible: true, isExpanded: false);
        section.ApplySearch(" 24W14A ");
        Assert.IsTrue(section.IsSearchMatch);
        Assert.IsTrue(section.IsExpanded);
        Assert.IsFalse(section.CanCollapse);
        Assert.AreSame(snapshot, section.Items.Single());
        section.ApplySearch("missing");
        Assert.IsFalse(section.IsSearchMatch);
        Assert.AreEqual(0, section.Items.Count);
        section.ApplySearch(" ");
        CollectionAssert.AreEqual(new[] { first, snapshot }, section.Items.ToArray());
        Assert.IsTrue(section.IsSearchMatch);
        Assert.IsFalse(section.IsExpanded);
        Assert.IsTrue(section.CanCollapse);
    }

    [TestMethod]
    public async Task ManualLoaderCatalog_FiltersMinecraftVersionGroupsWithoutFetchingThem()
    {
        var loads = 0;
        var pending = new TaskCompletionSource<IReadOnlyList<DownloadCatalogEntryViewModel>>();
        var section = new DownloadCatalogSectionViewModel("1.8.9", [], isCollapsible: true, isExpanded: false,
            loadEntriesAsync: _ => { loads++; return pending.Task; });
        section.ApplySearch("1.8.9");
        Assert.IsTrue(section.IsSearchMatch);
        Assert.AreEqual(0, loads);
        section.ApplySearch("1.20");
        Assert.IsFalse(section.IsSearchMatch);
        Assert.AreEqual(0, loads);

        section.ApplySearch("1.8.9");
        section.IsExpanded = true;
        Assert.AreEqual(1, loads);
        section.ApplySearch("missing");
        pending.SetResult([CreateEntry("11.15.1.2318", "Forge for 1.8.9")]);
        var deadline = DateTime.UtcNow.AddSeconds(3);
        while (section.IsLoading && DateTime.UtcNow < deadline) await Task.Delay(10);
        Assert.IsFalse(section.IsLoading);
        Assert.IsFalse(section.IsSearchMatch, "A late response must respect the latest search.");
        Assert.AreEqual(0, section.Items.Count);
        section.ApplySearch("11.15");
        Assert.IsTrue(section.IsSearchMatch);
        Assert.AreEqual(1, section.Items.Count);
        section.ApplySearch(string.Empty);
        Assert.AreEqual(1, loads, "Clearing the query must reuse the loaded entries.");
    }

    [TestMethod]
    public async Task ManualLoaderCatalog_ClickingAFilteredGroupLoadsItsVersions()
    {
        await FrontendMotionTest.RunAsync(async () =>
        {
            using var fixture = new ProfileFixture();
            var loads = 0;
            var section = new DownloadCatalogSectionViewModel("1.8.9", [], isCollapsible: true, isExpanded: false,
                loadEntriesAsync: _ =>
                {
                    loads++;
                    return Task.FromResult<IReadOnlyList<DownloadCatalogEntryViewModel>>([CreateEntry("11.15.1.2318", "Forge for 1.8.9")]);
                });
            fixture.Launcher.DownloadCatalogSections.Add(section);
            fixture.Launcher.DownloadCatalogSearchQuery = "1.8.9";
            var view = new DownloadCatalogRightPaneView { DataContext = fixture.Launcher };
            var window = new Window { Width = 700, Height = 450, Content = view };
            try
            {
                window.Show();
                Tick();
                var card = view.GetVisualDescendants().OfType<PclCard>().Single(item => ReferenceEquals(item.DataContext, section));
                Assert.AreEqual(0, loads);
                Assert.IsTrue(card.ShowChevron, "Unloaded groups still need an action to load their versions.");
                card.FindControl<Button>("HeaderButton")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Tick();
                await Task.Yield();
                Assert.AreEqual(1, loads, "The card's expansion must reach the section view model.");
                Assert.IsTrue(section.IsExpanded);
                Assert.IsFalse(card.ShowChevron, "Loaded search results must have a static header.");
                Assert.AreEqual("11.15.1.2318", section.Items.Single().Title);
                fixture.Launcher.DownloadCatalogSearchQuery = "missing";
                Tick();
                Assert.IsFalse(card.IsVisible);
                Assert.IsTrue(fixture.Launcher.ShowDownloadCatalogSearchEmpty);
                fixture.Launcher.DownloadCatalogSearchQuery = string.Empty;
                Tick();
                Assert.IsTrue(card.IsVisible);
                Assert.IsFalse(card.IsChevronExpanded);
                Assert.IsTrue(card.ShowChevron);
            }
            finally
            {
                window.Close();
            }
        });
    }

    private static FrontendInstallChoice CreateChoice(string version, string group) =>
        new(version, version, version, version, FrontendInstallChoiceKind.Minecraft,
            Metadata: new JsonObject { ["catalogGroup"] = group });

    private static DownloadCatalogEntryViewModel CreateEntry(string title, string info) =>
        new(title, info, string.Empty, "Download", new ActionCommand(() => { }));
}
