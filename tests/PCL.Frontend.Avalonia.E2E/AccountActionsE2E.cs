using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Styling;
using Avalonia.VisualTree;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.Minecraft.Launch;
using PCL.Frontend.Avalonia.Workflows;
using static PCL.Frontend.Avalonia.E2E.DesktopFixture;

namespace PCL.Frontend.Avalonia.E2E;

[TestClass]
[DoNotParallelize]
public sealed class AccountActionsE2E
{
    [TestMethod]
    public Task AccountActions_Hover_Keyboard_Edit_Select_AndAdd() => Run(async () =>
    {
        await using var app = new DesktopFixture();
        var document = FrontendProfileStorageService.CreateOfflineProfile(MinecraftLaunchProfileDocument.Empty,
            "TheUnknownThing", "0123456789abcdef0123456789abcdef");
        document = document with { Profiles = [document.Profiles[0] with { SkinHeadId = "Alex" }] };
        FrontendProfileStorageService.Save(app.Paths, document);
        await app.Start(); await Settle();
        var summary = app.Window.Find<StackPanel>("AccountSummary")!;
        var actions = app.Window.Find<Border>("AccountActions")!;
        var avatar = app.Window.Find<Image>("AccountAvatar")!;
        var outside = new Point(app.Window.Bounds.Width - 40, 120);
        app.Window.MouseMove(outside); await Settle();
        Assert.AreEqual(0d, actions.Opacity);
        Assert.IsFalse(actions.IsHitTestVisible, "Hidden account actions must not intercept mouse clicks.");
        app.Screenshot("account-actions-idle");
        var avatarOrigin = avatar.TranslatePoint(default, app.Window);
        await HoverAccount();
        Assert.AreEqual(avatarOrigin, avatar.TranslatePoint(default, app.Window), "Revealing actions must not move the account.");
        Assert.AreEqual(3, actions.GetVisualDescendants().OfType<PclIconButton>().Count());
        app.Screenshot("account-actions-hover");
        // Moving onto the actions must keep them visible so the third button remains reachable.
        var edit = app.Window.Find<PclIconButton>("EditAccountButton")!;
        app.Window.MouseMove(edit.TranslatePoint(new Point(edit.Bounds.Width / 2, edit.Bounds.Height / 2), app.Window)!.Value);
        await Settle(); Assert.AreEqual(1d, actions.Opacity);
        app.Window.MouseMove(outside); await Settle();
        Assert.AreEqual(0d, actions.Opacity);
        // Tab navigation must reveal controls even when the pointer is outside the account.
        for (var i = 0; i < 24 && !summary.IsKeyboardFocusWithin; i++)
        {
            app.Window.KeyPressQwerty(PhysicalKey.Tab, RawInputModifiers.None);
            app.Window.KeyReleaseQwerty(PhysicalKey.Tab, RawInputModifiers.None);
        }
        await Settle();
        Assert.IsTrue(summary.IsKeyboardFocusWithin);
        Assert.AreEqual(1d, actions.Opacity);
        await Click(app.Window, edit);
        Assert.IsTrue(app.Launcher.ShowLaunchOfflineEditor, "The edit icon must open the saved account's editor.");
        Assert.AreEqual("TheUnknownThing", app.Launcher.LaunchOfflineUserName);
        await Back(); await HoverAccount();
        await Click(app.Window, app.Window.Find<PclIconButton>("SelectAccountButton")!);
        Assert.IsTrue(app.Launcher.ShowLaunchProfileSelection);
        await Back(); await HoverAccount();
        await Click(app.Window, app.Window.Find<PclIconButton>("AddAccountButton")!);
        Assert.IsTrue(app.Launcher.ShowLaunchProfileChooser);
        await Back();
        app.Actions.PersistSharedValue("UiDarkMode", 1);
        FrontendAppearanceService.ApplyStoredAppearance(Application.Current!, app.Paths);
        await HoverAccount();
        Assert.AreEqual(ThemeVariant.Dark, app.Window.ActualThemeVariant);
        app.Screenshot("account-actions-dark");
        Assert.AreEqual(document.Profiles[0], FrontendProfileStorageService.Load(app.Paths).Document.Profiles[0],
            "Opening and closing account actions must leave the saved profile intact.");

        async Task HoverAccount()
        {
            var point = avatar.TranslatePoint(new Point(avatar.Bounds.Width / 2, avatar.Bounds.Height / 2), app.Window)!.Value;
            app.Window.MouseMove(point);
            await Wait(() => actions.IsHitTestVisible && actions.Opacity > .99, "Account actions did not appear on hover.");
            await Settle();
        }
        async Task Back()
        {
            var button = app.Window.GetVisualDescendants().OfType<PclButton>().Single(control => control.IsEffectivelyVisible
                && ReferenceEquals(control.Command, app.Launcher.BackLaunchProfileCommand));
            await Click(app.Window, button);
            Assert.IsTrue(app.Launcher.ShowLaunchProfileSummaryCard);
        }
    });
}
