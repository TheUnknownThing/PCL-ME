using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.VisualTree;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.Minecraft.Launch;
using PCL.Frontend.Avalonia.Desktop.Controls;
using PCL.Frontend.Avalonia.Workflows;
using static PCL.Frontend.Avalonia.E2E.DesktopFixture;

namespace PCL.Frontend.Avalonia.E2E;

[TestClass]
[DoNotParallelize]
public sealed class AccountSelectionPopupE2E
{
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public Task SelectAccount_UsesThePopup_AndSavesMouseOrKeyboardSelection(bool keyboard) => Run(async () =>
    {
        await using var app = new DesktopFixture();
        SaveProfiles(app, 2);
        await app.Start();
        await Settle();
        var avatar = app.Window.Find<Image>("AccountAvatar")!;
        var origin = avatar.TranslatePoint(default, app.Window);
        app.Window.MouseMove(avatar.TranslatePoint(new Point(36, 36), app.Window)!.Value);
        await Wait(() => app.Window.Find<Border>("AccountActions") is { IsHitTestVisible: true, Opacity: > .99 }, "Account actions did not appear on hover.");
        await Settle();
        var select = app.Window.Find<PclIconButton>("SelectAccountButton")!;
        var lifetime = (IClassicDesktopStyleApplicationLifetime)Application.Current!.ApplicationLifetime!;
        var windowCount = lifetime.Windows.Count;
        await Click(app.Window, select);
        var overlay = await WaitForSelection(app);
        Assert.AreEqual(app.Text.T("launch.profile.selection.prompt_title"), overlay.Find<TextBlock>("LoginPopupTitle")!.Text);
        Assert.AreEqual(windowCount, lifetime.Windows.Count);
        Assert.AreSame(app.Window, TopLevel.GetTopLevel(overlay));
        Assert.IsFalse(app.Launcher.IsTopLevelNavigationInteractive);
        Assert.IsTrue(app.Window.Find<StackPanel>("AccountSummary")!.IsEffectivelyVisible);
        Assert.AreEqual(origin, avatar.TranslatePoint(default, app.Window));
        Assert.AreEqual("LoginBackdrop", (app.Window.InputHitTest(avatar.TranslatePoint(new Point(36, 36), app.Window)!.Value) as Control)?.Name);
        Assert.IsFalse(app.Window.GetVisualDescendants().OfType<PclLaunchLeftPanel>().Single()
            .GetVisualDescendants().OfType<PclListItem>().Any(), "Saved accounts must be listed inside the popup.");
        var rows = overlay.Find<ItemsControl>("AccountSelectionEntries")!.GetVisualDescendants().OfType<PclListItem>().ToArray();
        Assert.AreEqual(2, rows.Length);
        Assert.IsTrue(rows[1].IsSelected);
        Assert.IsTrue(rows[1].IsFocused, "The current account should receive keyboard focus.");
        app.Screenshot("account-selection-popup");
        for (var i = 0; i < 12; i++)
        {
            app.Window.KeyPressQwerty(PhysicalKey.Tab, RawInputModifiers.None);
            app.Window.KeyReleaseQwerty(PhysicalKey.Tab, RawInputModifiers.None);
            Assert.IsTrue(overlay.IsKeyboardFocusWithin, "Tab navigation must stay inside account selection.");
        }
        if (keyboard)
        {
            rows[0].Focus();
            app.Window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            app.Window.KeyReleaseQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        }
        else await Click(app.Window, rows[0]);
        await Wait(() => !app.Launcher.IsLaunchProfilePopupVisible, "Choosing an account did not close the popup.");
        await Wait(() => !overlay.Find<Grid>("OverlayRoot")!.IsVisible, "The account popup did not finish closing.");
        Assert.AreEqual(0, FrontendProfileStorageService.Load(app.Paths).Document.LastUsedProfile);
        Assert.AreEqual("SavedPlayer00", app.Launcher.LaunchUserName);
        Assert.IsTrue(app.Launcher.IsTopLevelNavigationInteractive);

        app.Launcher.SelectLaunchProfileCommand.Execute(null);
        await WaitForSelection(app);
        app.Window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        app.Window.KeyReleaseQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        await Wait(() => !overlay.Find<Grid>("OverlayRoot")!.IsVisible, "Escape did not dismiss account selection.");
        Assert.AreEqual(0, FrontendProfileStorageService.Load(app.Paths).Document.LastUsedProfile);
    });

    [TestMethod]
    [DataRow("de-DE", false)]
    [DataRow("zh-Hans", true)]
    public Task SelectAccount_LongListsScroll_AndAddStaysReachableInAShortWindow(string locale, bool dark) => Run(async () =>
    {
        await using var app = new DesktopFixture();
        SaveProfiles(app, 18);
        await app.Start();
        app.Text.SetLocale(locale);
        app.Actions.PersistSharedValue("UiDarkMode", dark ? 1 : 0);
        FrontendAppearanceService.ApplyStoredAppearance(Application.Current!, app.Paths);
        app.Window.Height = app.Window.MinHeight;
        app.Launcher.SelectLaunchProfileCommand.Execute(null);
        var overlay = await WaitForSelection(app);
        var entries = overlay.Find<ItemsControl>("AccountSelectionEntries")!;
        var scroll = entries.GetVisualAncestors().OfType<ScrollViewer>().First();
        Assert.IsTrue(scroll.Extent.Height > scroll.Viewport.Height, "Long account lists must scroll inside the popup.");
        Assert.IsTrue(overlay.Find<Border>("LoginCard")!.Bounds.Height <= app.Window.Bounds.Height - 56);
        var last = entries.GetVisualDescendants().OfType<PclListItem>().Last();
        last.BringIntoView();
        await Settle();
        Assert.IsTrue(scroll.Offset.Y > 0);
        app.Screenshot($"account-selection-compact-{locale}-{(dark ? "dark" : "light")}");
        await Click(app.Window, overlay.Find<PclButton>("AddAccountFromSelectionButton")!);
        await Wait(() => overlay.Find<Grid>("LoginMethodChooser")!.IsEffectivelyVisible, "Add did not open login methods in the popup.");
        Assert.IsTrue(overlay.IsKeyboardFocusWithin, "Changing steps must keep focus inside the popup.");
        Assert.IsTrue(overlay.Find<Button>("MicrosoftLoginMethod")!.IsFocused, "The login chooser must receive focus after leaving selection.");
        await Click(app.Window, overlay.Find<PclButton>("CloseLoginPopupButton")!);
        Assert.IsFalse(app.Launcher.IsLaunchProfilePopupVisible);
        Assert.AreEqual(18, FrontendProfileStorageService.Load(app.Paths).Document.Profiles.Count);
    });

    [TestMethod]
    public Task SelectAccount_DeletingTheLastAccountShowsAnEmptyPopup_ThatCanBeDismissed() => Run(async () =>
    {
        await using var app = new DesktopFixture();
        await app.Start();
        Assert.IsFalse(app.Launcher.IsLaunchProfilePopupVisible, "Starting without accounts must leave the launcher usable.");
        await Click(app.Window, app.Window.Find<PclIconButton>("AddFirstAccountButton")!);
        await Wait(() => app.Launcher.ShowLaunchProfileChooser, "The empty sidebar must offer account creation.");
        app.Launcher.BackLaunchProfileCommand.Execute(null);
        await Wait(() => !app.Window.Find<LaunchProfileLoginOverlay>("LoginOverlay")!.Find<Grid>("OverlayRoot")!.IsVisible, "Cancel did not close the empty-account login popup.");

        SaveProfiles(app, 1);
        app.Launcher.SelectLaunchProfileCommand.Execute(null);
        var overlay = await WaitForSelection(app);
        var row = overlay.Find<ItemsControl>("AccountSelectionEntries")!.GetVisualDescendants().OfType<PclListItem>().Single();
        await Click(app.Window, row.Find<Button>("SecondaryAccessoryButton")!);
        await Wait(() => app.Launcher.IsPromptOverlayVisible, "Deleting an account must ask for confirmation.");
        Assert.IsTrue(app.Launcher.IsLaunchProfilePopupVisible, "Confirmation must appear above account selection.");
        await Click(app.Window, app.Window.GetVisualDescendants().OfType<PclButton>()
            .Single(button => button.IsEffectivelyVisible && button.Text == app.Text.T("launch.profile.delete.confirmation.confirm")));
        await Wait(() => app.Launcher.HasNoLaunchProfileEntries && app.Launcher.BackLaunchProfileCommand.CanExecute(null), "Deleting the last account did not update the list.");
        Assert.IsTrue(app.Launcher.IsLaunchProfilePopupVisible);
        Assert.IsTrue(app.Window.Find<StackPanel>("AccountEmptyState")!.IsEffectivelyVisible);
        app.Screenshot("account-selection-empty");
        await Click(app.Window, overlay.Find<PclButton>("CloseLoginPopupButton")!);
        await Wait(() => !overlay.Find<Grid>("OverlayRoot")!.IsVisible, "The empty account popup could not be dismissed.");
        Assert.IsTrue(app.Launcher.IsTopLevelNavigationInteractive);
        Assert.AreEqual(0, FrontendProfileStorageService.Load(app.Paths).Document.Profiles.Count);
        app.Launcher.SelectLaunchProfileCommand.Execute(null);
        await WaitForSelection(app);
        await Click(app.Window, overlay.Find<PclButton>("AddAccountFromSelectionButton")!);
        Assert.IsTrue(app.Launcher.ShowLaunchProfileChooser);
        await Click(app.Window, overlay.Find<PclButton>("CloseLoginPopupButton")!);
        Assert.IsFalse(app.Launcher.IsLaunchProfilePopupVisible);
    });

    private static async Task<LaunchProfileLoginOverlay> WaitForSelection(DesktopFixture app)
    {
        await Wait(() => app.Window.Find<Border>("LoginCard") is { IsEffectivelyVisible: true, Opacity: > .99 }, "Account selection popup did not open.");
        await Settle();
        var overlay = app.Window.Find<LaunchProfileLoginOverlay>("LoginOverlay")!;
        Assert.IsTrue(overlay.Find<StackPanel>("AccountSelection")!.IsEffectivelyVisible);
        return overlay;
    }

    private static void SaveProfiles(DesktopFixture app, int count)
    {
        var document = MinecraftLaunchProfileDocument.Empty;
        for (var index = 0; index < count; index++)
            document = FrontendProfileStorageService.CreateOfflineProfile(document, $"SavedPlayer{index:D2}", index.ToString("x32"));
        FrontendProfileStorageService.Save(app.Paths, document);
    }
}
