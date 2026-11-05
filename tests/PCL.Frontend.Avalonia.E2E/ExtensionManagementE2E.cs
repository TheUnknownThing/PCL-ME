using System.IO.Compression;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Input.Raw;
using Avalonia.Media;
using Avalonia.VisualTree;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Extensions.Contracts;
using PCL.Extensions.Runtime;
using PCL.Frontend.Avalonia.Desktop.Controls;
using PCL.Frontend.Avalonia.Desktop.Extensions;
using PCL.Frontend.Avalonia.Workflows;
using static PCL.Frontend.Avalonia.E2E.DesktopFixture;

namespace PCL.Frontend.Avalonia.E2E;

[TestClass]
[DoNotParallelize]
public sealed class ExtensionManagementE2E
{
    [TestMethod]
    public Task CardHeader_Actions_ReflectEnabledState_AndOpenInWindowInformationAndLogs() => Run(async () =>
    {
        await using var app = new DesktopFixture();
        await app.Start(); await app.OpenExtensions();
        var view = app.Card("pcl.desktop-pet");
        var card = view.Find<PclCard>("Card")!;
        var actions = view.Find<StackPanel>("HeaderActions")!;
        CollectionAssert.AreEqual(new[] { "ToggleEnabledButton", "InfoButton" },
            actions.Children.Select(control => control.Name).ToArray());
        Assert.IsTrue(actions.Children.All(control => control.IsVisible));
        var title = card.FindControl<TextBlock>("HeaderTextBlock")!;
        var disabledColor = ((ISolidColorBrush)title.Foreground!).Color;
        Assert.IsTrue(title.Opacity < 1);
        var titlePoint = title.TranslatePoint(new Point(10, 5), app.Window)!.Value;
        app.Window.MouseMove(titlePoint); await Settle();
        Assert.AreEqual(disabledColor, ((ISolidColorBrush)title.Foreground!).Color, "Hover must preserve the muted disabled title.");
        await app.Toggle("pcl.desktop-pet");
        Assert.AreEqual(1d, title.Opacity);
        Assert.AreNotEqual(disabledColor, ((ISolidColorBrush)title.Foreground!).Color);
        await app.Toggle("pcl.desktop-pet");
        Assert.AreEqual(disabledColor, ((ISolidColorBrush)title.Foreground!).Color);
        await app.Expand("pcl.desktop-pet");
        Assert.IsFalse(view.Find<PclButton>("UpdateButton")!.IsEnabled);
        Assert.IsFalse(view.Find<PclButton>("UninstallButton")!.IsEnabled);
        Assert.IsFalse(view.GetVisualDescendants().OfType<TextBlock>().Any(text => text.IsEffectivelyVisible && text.Text is "Disabled" or "Running"));
        await Click(app.Window, view.Find<PclIconButton>("InfoButton")!);
        var info = (ExtensionDetailsView)app.Session.OverlayContent!;
        Assert.AreSame(app.Window, TopLevel.GetTopLevel(info));
        Assert.AreEqual("pcl.desktop-pet", info.Find<TextBlock>("IdValueText")!.Text);
        Assert.AreEqual("PCL-ME Developers", info.Find<TextBlock>("AuthorValueText")!.Text);
        Assert.IsTrue(info.Find<Border>("BuiltInBadge")!.IsVisible, "Bundled extensions must show the built-in badge.");
        Assert.IsFalse(info.Find<HyperlinkButton>("ExtensionLink")!.IsVisible);
        Assert.IsFalse(info.Find<HyperlinkButton>("AuthorLink")!.IsVisible);
        await app.Window.Clipboard!.SetTextAsync("previous clipboard text");
        var copyId = info.Find<Button>("CopyIdButton")!;
        Assert.IsNull(copyId.FocusAdorner);
        Assert.AreEqual(new Thickness(0), copyId.BorderThickness);
        await Click(app.Window, info.Find<TextBlock>("IdValueText")!);
        Assert.AreEqual("pcl.desktop-pet", await app.Window.Clipboard.TryGetTextAsync());
        Assert.AreEqual(0, info.Find<TextBlock>("IdValueText")!.TextDecorations?.Count ?? 0, "The ID must remain plain text on hover.");
        Assert.AreEqual(app.Text.T("extensions.copy_id"), ToolTip.GetTip(copyId));
        await app.Window.Clipboard.SetTextAsync("previous clipboard text");
        await Click(app.Window, info.Find<PathIcon>("CopyIdIcon")!);
        Assert.AreEqual("pcl.desktop-pet", await app.Window.Clipboard.TryGetTextAsync(), "Clicking the copy icon must copy the ID.");
        Assert.IsTrue(info.Find<TextBlock>("CopyFeedbackText")!.IsVisible);
        await Wait(() => !info.Find<TextBlock>("CopyFeedbackText")!.IsVisible, "Copy feedback did not clear.");
        await app.Window.Clipboard.SetTextAsync("previous clipboard text");
        copyId.Focus();
        app.Window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        app.Window.KeyReleaseQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        await Settle();
        Assert.AreEqual("pcl.desktop-pet", await app.Window.Clipboard.TryGetTextAsync(), "Keyboard activation must copy the ID.");
        await Wait(() => !info.Find<TextBlock>("CopyFeedbackText")!.IsVisible, "Copy feedback did not clear after keyboard activation.");
        info.Find<PclButton>("CloseButton")!.FindControl<Button>("ButtonHost")!.Focus();
        app.Window.MouseMove(new Point(10, 10)); await Settle();
        app.Screenshot("extensions-info-light");
        app.Actions.PersistSharedValue("UiDarkMode", 1);
        FrontendAppearanceService.ApplyStoredAppearance(global::Avalonia.Application.Current!, app.Paths);
        await Settle();
        app.Screenshot("extensions-info-dark");
        app.Window.Width = 810; app.Window.Height = 470;
        foreach (var locale in new[] { "en-US", "zh-Hans", "zh-Hant", "lzh", "de-DE", "fr-FR", "es-ES", "ko-KR" })
        {
            app.Text.SetLocale(locale); await Settle();
            var surface = info.Find<Border>("DetailsSurface")!;
            var start = surface.TranslatePoint(default, app.Window)!.Value;
            Assert.IsTrue(start.X >= 0 && start.Y >= 0 && start.X + surface.Bounds.Width <= app.Window.Bounds.Width
                && start.Y + surface.Bounds.Height <= app.Window.Bounds.Height, "Extension information must fit the minimum window in " + locale);
            var close = info.Find<PclButton>("CloseButton")!;
            var corner = close.TranslatePoint(new Point(close.Bounds.Width, close.Bounds.Height), surface)!.Value;
            Assert.IsTrue(corner.X <= surface.Bounds.Width && corner.Y <= surface.Bounds.Height,
                "The close action must remain visible in " + locale);
        }
        app.Text.SetLocale("en-US"); await Settle();
        app.Screenshot("extensions-info-dark-compact");
        app.Window.Width = 1000; app.Window.Height = 700;
        app.Window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        app.Window.KeyReleaseQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        await Settle();
        Assert.IsNull(app.Session.OverlayContent);
        app.Session.Host.LogContributionError("pcl.global-search", new InvalidOperationException("OTHER_EXTENSION_LOG"));
        app.Session.Host.LogContributionError("pcl.desktop-pet", new InvalidOperationException("DESKTOP_PET_LOG"));
        await Click(app.Window, view.Find<PclButton>("LogButton")!);
        var logs = (ExtensionDetailsView)app.Session.OverlayContent!;
        Assert.AreSame(app.Window, TopLevel.GetTopLevel(logs));
        var logText = logs.Find<TextBox>("LogText")!;
        StringAssert.Contains(logText.Text, "DESKTOP_PET_LOG");
        Assert.IsFalse(logText.Text!.Contains("OTHER_EXTENSION_LOG", StringComparison.Ordinal));
        app.Session.Host.LogContributionError("pcl.desktop-pet", new InvalidOperationException("LIVE_LOG_UPDATE"));
        await Wait(() => logText.Text!.Contains("LIVE_LOG_UPDATE", StringComparison.Ordinal), "Logs must refresh while open.");
        await Click(app.Window, logs.Find<PclButton>("CloseButton")!);
        app.Actions.PersistSharedValue("UiDarkMode", 1);
        FrontendAppearanceService.ApplyStoredAppearance(global::Avalonia.Application.Current!, app.Paths);
        app.Window.Width = 1000; app.Window.Height = 840;
        await Settle();
        titlePoint = title.TranslatePoint(new Point(10, 5), app.Window)!.Value;
        app.Window.MouseMove(titlePoint); await Settle();
        app.Screenshot("extensions-card-disabled-actions");
        await Click(app.Window, view.Find<PclButton>("LogButton")!);
        app.Screenshot("extensions-card-logs");
    });

    [TestMethod]
    public Task Information_OptionalWebsiteLinks_UseManifestUrls() => Run(async () =>
    {
        await using var app = new DesktopFixture();
        await app.Start(); await app.OpenExtensions();
        var cases = new (string? Website, string? AuthorWebsite, bool ExtensionLink, bool AuthorLink)[]
        {
            ("https://example.com/extension", "https://example.com/author", true, true),
            ("https://example.com/extension", null, true, false),
            (null, "http://example.com/author", false, true),
            ("not a URL", "file:///tmp/author", false, false),
            (null, null, false, false)
        };
        for (var i = 0; i < cases.Length; i++)
        {
            var item = cases[i];
            await Install(app, CreatePackage(app, "1.0." + i, item.Website, item.AuthorWebsite, "Example author"));
            var installed = app.Session.Host.Get("example.hello").Manifest;
            Assert.AreEqual(item.AuthorWebsite, installed.AuthorWebsite, "Author links must survive package serialization and installation.");
            await Click(app.Window, app.Card("example.hello").Find<PclIconButton>("InfoButton")!);
            var info = (ExtensionDetailsView)app.Session.OverlayContent!;
            var extension = info.Find<HyperlinkButton>("ExtensionLink")!;
            var author = info.Find<HyperlinkButton>("AuthorLink")!;
            Assert.AreEqual(item.ExtensionLink, extension.IsVisible);
            Assert.AreEqual(item.AuthorLink, author.IsVisible);
            Assert.IsTrue(info.Find<TextBlock>("NameText")!.IsVisible, "The title must always remain plain text.");
            Assert.AreEqual(!item.AuthorLink, info.Find<TextBlock>("AuthorValueText")!.IsVisible);
            if (item.ExtensionLink)
            {
                Assert.AreEqual(new Uri(item.Website!), extension.NavigateUri);
                var activated = false;
                extension.Click += (_, _) => activated = true;
                await Click(app.Window, info.Find<TextBlock>("NameText")!);
                Assert.IsFalse(activated, "Only the website icon should open the project page.");
                Assert.AreEqual(0, info.Find<TextBlock>("NameText")!.TextDecorations?.Count ?? 0);
                await Click(app.Window, extension);
                Assert.IsTrue(activated, "The extension website must be clickable.");
            }
            if (item.AuthorLink)
            {
                Assert.AreEqual(new Uri(item.AuthorWebsite!), author.NavigateUri);
                var activated = false;
                author.Click += (_, _) => activated = true;
                author.Focus();
                app.Window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
                app.Window.KeyReleaseQwerty(PhysicalKey.Enter, RawInputModifiers.None);
                await Settle();
                Assert.IsTrue(activated, "Author links must support keyboard activation.");
            }
            if (i == 0)
            {
                info.Find<PclButton>("CloseButton")!.FindControl<Button>("ButtonHost")!.Focus();
                app.Window.MouseMove(new Point(10, 10)); await Settle();
                app.Screenshot("extensions-info-links-light");
                app.Actions.PersistSharedValue("UiDarkMode", 1);
                FrontendAppearanceService.ApplyStoredAppearance(global::Avalonia.Application.Current!, app.Paths);
                await Settle();
                app.Screenshot("extensions-info-links-dark");
            }
            await Click(app.Window, info.Find<PclButton>("CloseButton")!);
        }
    });

    [TestMethod]
    public Task UrlInstaller_IsAnInWindowModal_ContainsDropdowns_AndRestoresFocus() => Run(async () =>
    {
        await using var app = new DesktopFixture();
        await app.Start(); await app.OpenExtensions();
        var manager = app.Window.GetVisualDescendants().OfType<ExtensionManagerView>().Single();
        var install = manager.Find<PclButton>("InstallUrlButton")!;
        var focus = install.GetVisualDescendants().OfType<Button>().Single();
        var lifetime = (IClassicDesktopStyleApplicationLifetime)global::Avalonia.Application.Current!.ApplicationLifetime!;
        var windowCount = lifetime.Windows.Count;
        focus.Focus();
        await Click(app.Window, install);
        var installer = (ExtensionUrlInstallView)app.Session.OverlayContent!;
        Assert.AreSame(app.Window, TopLevel.GetTopLevel(installer));
        Assert.IsTrue(installer.GetVisualAncestors().Any(visual => visual is Grid { Name: "ExtensionOverlayHost" }));
        Assert.AreEqual(windowCount, lifetime.Windows.Count, "Opening a popup must not create another window.");
        Assert.IsTrue(installer.Find<TextBox>("UrlInput")!.IsFocused);
        var background = app.Window.GetVisualDescendants().OfType<PclListItem>().Single(item => item.Title == "Tests");
        var point = background.TranslatePoint(new Point(background.Bounds.Width / 2, background.Bounds.Height / 2), app.Window)!.Value;
        app.Window.MouseDown(point, MouseButton.Left, RawInputModifiers.None);
        app.Window.MouseUp(point, MouseButton.Left, RawInputModifiers.None);
        await Settle();
        Assert.AreEqual("Tools/ToolsExtensions", app.Launcher.ExtensionRoute, "The popup must block interactions with the page behind it.");
        Assert.AreSame(installer, app.Session.OverlayContent);
        app.Window.KeyPressQwerty(PhysicalKey.P, RawInputModifiers.Control | RawInputModifiers.Shift);
        app.Window.KeyReleaseQwerty(PhysicalKey.P, RawInputModifiers.None);
        Assert.AreSame(installer, app.Session.OverlayContent, "Global search must not replace the install dialog.");
        var channel = installer.Find<ComboBox>("ChannelInput")!;
        await Click(app.Window, channel);
        var popup = channel.GetVisualDescendants().OfType<Popup>().Single();
        Assert.IsTrue(popup.ShouldUseOverlayLayer, "Dropdowns must use an in-window popup host.");
        Assert.AreSame(app.Window, TopLevel.GetTopLevel(popup.Child!));
        app.Window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        app.Window.KeyReleaseQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        await Click(app.Window, installer.Find<TextBox>("UrlInput")!);
        app.Window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        app.Window.KeyReleaseQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        await Wait(() => app.Session.OverlayContent is null, "Escape did not dismiss the popup.");
        Assert.AreSame(focus, app.Window.FocusManager!.GetFocusedElement());
        await Click(app.Window, install);
        installer = (ExtensionUrlInstallView)app.Session.OverlayContent!;
        app.Window.Width = 810; app.Window.Height = 470;
        app.Actions.PersistSharedValue("UiDarkMode", 1);
        FrontendAppearanceService.ApplyStoredAppearance(global::Avalonia.Application.Current!, app.Paths);
        await Click(app.Window, installer.Find<RadioButton>("SourceChoice")!);
        await Settle();
        var surface = installer.Find<Border>("UrlInstallSurface")!;
        var start = surface.TranslatePoint(default, app.Window)!.Value;
        Assert.IsTrue(start.X >= 0 && start.Y >= 0 && start.X + surface.Bounds.Width <= app.Window.Bounds.Width
            && start.Y + surface.Bounds.Height <= app.Window.Bounds.Height, "The source popup must fit inside the minimum window size.");
        app.Screenshot("extensions-url-popup-dark");
        await Click(app.Window, installer.Find<PclIconButton>("CloseInstallButton")!);
        Assert.IsNull(app.Session.OverlayContent);
    });

    [TestMethod]
    public Task TrustWarning_ShowsOnFirstVisit_IsInWindow_AndAcknowledgesOnce() => Run(async () =>
    {
        await using var app = new DesktopFixture();
        await app.Start();
        var lifetime = (IClassicDesktopStyleApplicationLifetime)global::Avalonia.Application.Current!.ApplicationLifetime!;
        var windowCount = lifetime.Windows.Count;
        await app.OpenExtensions(dismissTrustWarning: false);
        var warning = (ExtensionTrustWarningView)app.Session.OverlayContent!;
        Assert.AreSame(app.Window, TopLevel.GetTopLevel(warning));
        Assert.IsTrue(warning.GetVisualAncestors().Any(visual => visual is Grid { Name: "ExtensionOverlayHost" }));
        Assert.AreEqual(windowCount, lifetime.Windows.Count, "The trust warning must not create another window.");
        Assert.AreEqual(app.Text.T("extensions.trust_warning.title"), warning.Find<TextBlock>("WarningTitle")!.Text);
        Assert.AreEqual(app.Text.T("extensions.trust_warning.message"), warning.Find<TextBlock>("WarningText")!.Text);
        Assert.AreEqual(app.Text.T("extensions.trust_warning.acknowledge"), warning.Find<PclButton>("AcknowledgeButton")!.Text);
        // Outside clicks and Escape must not dismiss the warning or interact with the page behind it.
        var background = app.Window.GetVisualDescendants().OfType<PclListItem>().Single(item => item.Title == "Tests");
        var point = background.TranslatePoint(new Point(background.Bounds.Width / 2, background.Bounds.Height / 2), app.Window)!.Value;
        app.Window.MouseDown(point, MouseButton.Left, RawInputModifiers.None);
        app.Window.MouseUp(point, MouseButton.Left, RawInputModifiers.None);
        app.Window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        app.Window.KeyReleaseQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        await Settle();
        Assert.AreSame(warning, app.Session.OverlayContent, "The trust warning must only close through its acknowledge button.");
        Assert.AreEqual("Tools/ToolsExtensions", app.Launcher.ExtensionRoute, "The warning must block interactions with the page behind it.");
        // The warning restyles and fits the minimum window in every locale.
        app.Window.Width = 810; app.Window.Height = 470;
        foreach (var darkMode in new[] { 0, 1 })
        {
            app.Actions.PersistSharedValue("UiDarkMode", darkMode);
            FrontendAppearanceService.ApplyStoredAppearance(global::Avalonia.Application.Current!, app.Paths);
            foreach (var locale in new[] { "en-US", "zh-Hans", "zh-Hant", "lzh", "de-DE", "fr-FR", "es-ES", "ko-KR" })
            {
                app.Text.SetLocale(locale); await Settle();
                foreach (var (name, key) in new[] { ("WarningTitle", "title"), ("WarningText", "message") })
                {
                    var text = warning.Find<TextBlock>(name)!;
                    Assert.AreEqual(app.Text.T("extensions.trust_warning." + key), text.Text,
                        "The trust warning must follow the active locale.");
                    Assert.IsTrue(text.TextLayout.Height <= text.Bounds.Height + 1, $"{name} must not be clipped in {locale}.");
                }
                var surface = warning.Find<Border>("WarningSurface")!;
                var start = surface.TranslatePoint(default, app.Window)!.Value;
                Assert.IsTrue(start.X >= 0 && start.Y >= 0 && start.X + surface.Bounds.Width <= app.Window.Bounds.Width
                    && start.Y + surface.Bounds.Height <= app.Window.Bounds.Height, "The trust warning must fit the minimum window in " + locale);
                var scroll = warning.GetVisualDescendants().OfType<ScrollViewer>().Single();
                Assert.IsTrue(scroll.Extent.Height <= scroll.Viewport.Height + 1, "The warning must be readable without scrolling in " + locale);
            }
            app.Text.SetLocale("en-US"); await Settle();
            app.Screenshot("extensions-trust-warning-" + (darkMode == 0 ? "light" : "dark"));
            app.Text.SetLocale("zh-Hans"); await Settle();
            app.Screenshot("extensions-trust-warning-zh-Hans-" + (darkMode == 0 ? "light" : "dark"));
            app.Text.SetLocale("en-US"); await Settle();
        }
        await Click(app.Window, warning.Find<PclButton>("AcknowledgeButton")!);
        await Wait(() => app.Session.OverlayContent is null, "The trust warning did not close after acknowledging it.");
        Assert.IsTrue(app.Actions.HasAcknowledgedExtensionTrustWarning(), "Acknowledging must persist the dismissal.");
        // Re-entering the page must not show the warning again.
        await Click(app.Window, app.Window.GetVisualDescendants().OfType<PclListItem>().Single(item => item.Title == "Tests"));
        await Wait(() => !app.Window.GetVisualDescendants().OfType<ExtensionManagerView>().Any(), "Leaving the Extensions tool did not release its view.");
        await app.OpenExtensions();
        Assert.IsNull(app.Session.OverlayContent, "The trust warning must only appear on the first visit.");
        await app.Restart(); await app.OpenExtensions();
        Assert.IsNull(app.Session.OverlayContent, "The acknowledgment must survive a restart.");
    });

    [TestMethod]
    public Task Search_FiltersLocalizedNamesAndIds_PreservesEditors_AndHandlesEmptyResults() => Run(async () =>
    {
        await using var app = new DesktopFixture();
        await app.Start(); await app.OpenExtensions();
        await app.Expand("pcl.global-search");
        var manager = app.Window.GetVisualDescendants().OfType<ExtensionManagerView>().Single();
        var editor = app.Card("pcl.global-search").Find<NumericUpDown>("Setting_limit");
        var query = manager.Find<PclSearchBox>("ExtensionSearch")!.Find<TextBox>("SearchTextBox")!;
        await Type(app.Window, query, "PCL.GLOBAL-SEARCH");
        Assert.IsTrue(app.Card("pcl.global-search").IsVisible);
        Assert.IsFalse(app.Card("pcl.desktop-pet").IsVisible);
        Assert.AreEqual("1 of 2 extensions", manager.Find<TextBlock>("CountText")!.Text);
        await Type(app.Window, query, "no extension matches 0192");
        Assert.IsTrue(manager.Find<TextBlock>("EmptyText")!.IsVisible);
        await Click(app.Window, manager.Find<PclSearchBox>("ExtensionSearch")!.Find<PclIconButton>("ClearButton")!);
        Assert.AreSame(editor, app.Card("pcl.global-search").Find<NumericUpDown>("Setting_limit"));
        Assert.IsFalse(manager.Find<TextBlock>("EmptyText")!.IsVisible);
        app.Text.SetLocale("zh-Hans"); await Settle();
        await Type(app.Window, query, "桌宠");
        Assert.IsTrue(app.Card("pcl.desktop-pet").IsVisible);
        Assert.IsFalse(app.Card("pcl.global-search").IsVisible);
    });

    [TestMethod]
    public Task UrlPackage_InstallsFromHttp_ReportsFailure_AndCancelsDownload() => Run(async () =>
    {
        await using var app = new DesktopFixture();
        await using var repository = new ExtensionTestRepository();
        await app.Start(); await app.OpenExtensions();
        var manager = app.Window.GetVisualDescendants().OfType<ExtensionManagerView>().Single();
        await Click(app.Window, manager.Find<PclButton>("InstallUrlButton")!);
        var installer = (ExtensionUrlInstallView)app.Session.OverlayContent!;
        var url = repository.Add("/hello.pclext", File.ReadAllBytes(CreatePackage(app, "1.0.0")));
        await Type(app.Window, installer.Find<TextBox>("UrlInput")!, url);
        Assert.IsFalse(installer.Find<StackPanel>("RepositoryOptions")!.IsVisible);
        await Click(app.Window, installer.Find<PclButton>("ConfirmInstallButton")!);
        await Wait(() => app.Session.Host.Extensions.Any(info => info.Manifest.Id == "example.hello"), "URL package was not installed");
        Assert.IsFalse(app.Session.Host.Get("example.hello").IsEnabled);
        await Click(app.Window, manager.Find<PclButton>("InstallUrlButton")!);
        installer = (ExtensionUrlInstallView)app.Session.OverlayContent!;
        await Type(app.Window, installer.Find<TextBox>("UrlInput")!, repository.Add("/invalid.pclext", [1, 2, 3]));
        await Click(app.Window, installer.Find<PclButton>("ConfirmInstallButton")!);
        await Wait(() => installer.Find<TextBlock>("InstallErrorText")!.IsVisible, "Invalid package feedback was not shown");
        var gate = new TaskCompletionSource();
        await Type(app.Window, installer.Find<TextBox>("UrlInput")!, repository.Add("/waiting.pclext", [1, 2, 3], gate.Task));
        await Click(app.Window, installer.Find<PclButton>("ConfirmInstallButton")!);
        await Wait(() => repository.Requests.Contains("/waiting.pclext"), "Download did not start");
        await Click(app.Window, installer.Find<PclButton>("CancelInstallButton")!);
        await Wait(() => !installer.IsBusy, "Canceled download did not finish");
        Assert.IsNull(app.Session.OverlayContent);
        Assert.AreEqual(3, app.Session.Host.Extensions.Count);
        gate.SetResult();
    });

    [TestMethod]
    public Task RepositoryInstall_SelectsBetaRelease_AndCardUpdateBuildsSelectedSourceBranch() => Run(async () =>
    {
        await using var app = new DesktopFixture();
        await using var repository = new ExtensionTestRepository();
        await app.Start(); await app.OpenExtensions();
        var package = repository.Add("/hello.pclext", File.ReadAllBytes(CreatePackage(app, "1.0.0")));
        repository.AddJson("/api/v4/projects/owner%2Fproject/releases?per_page=100&limit=100&page=1", new[] { new
        {
            name = "1.0 Beta", tag_name = "v1.0.0-beta.1", assets = new { links = new[] { new { name = "hello.pclext", url = package } } }
        } });
        var manager = app.Window.GetVisualDescendants().OfType<ExtensionManagerView>().Single();
        await Click(app.Window, manager.Find<PclButton>("InstallUrlButton")!);
        var installer = (ExtensionUrlInstallView)app.Session.OverlayContent!;
        await Type(app.Window, installer.Find<TextBox>("UrlInput")!, repository.Url + "/owner/project");
        await Click(app.Window, installer.Find<PclButton>("LoadButton")!);
        await Wait(() => !installer.IsBusy, "Release options did not load");
        Assert.IsFalse(installer.Find<PclButton>("ConfirmInstallButton")!.IsEnabled);
        await Click(app.Window, installer.Find<ComboBox>("ChannelInput")!);
        app.Window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None); app.Window.KeyReleaseQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
        app.Window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None); app.Window.KeyReleaseQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        await Settle();
        Assert.AreEqual(1, installer.Find<ComboBox>("ReleaseInput")!.ItemCount);
        app.Screenshot("extensions-url-release");
        await Click(app.Window, installer.Find<PclButton>("ConfirmInstallButton")!);
        await Wait(() => app.Session.Host.Extensions.Any(info => info.Manifest.Id == "example.hello"), "Release package did not install");
        var card = app.Card("example.hello");
        var update = card.Find<PclButton>("UpdateButton")!;
        await Click(app.Window, update);
        var sourceUpdate = ((MenuFlyout)update.ContextFlyout!).Items.OfType<MenuItem>().Last();
        await Click(TopLevel.GetTopLevel(sourceUpdate)!, sourceUpdate);
        installer = (ExtensionUrlInstallView)app.Session.OverlayContent!;
        using var http = new HttpClient();
        string? builtBranch = null;
        var sourcePackage = CreatePackage(app, "2.0.0");
        installer.Service = new FrontendExtensionInstallService(http, (executable, arguments, directory, token) =>
        {
            if (arguments[0] == "ls-remote") return Task.FromResult("abc\trefs/heads/main\nabc\trefs/heads/develop\n");
            if (executable == "git")
            {
                builtBranch = arguments[Array.IndexOf(arguments, "--branch") + 1];
                Directory.CreateDirectory(arguments.Last());
                File.WriteAllText(Path.Combine(arguments.Last(), "Hello.csproj"), "<Project />");
                File.WriteAllText(Path.Combine(arguments.Last(), "extension.json"), "{\"id\":\"example.hello\"}");
            }
            else ZipFile.ExtractToDirectory(sourcePackage, arguments.Last());
            return Task.FromResult("");
        });
        await Type(app.Window, installer.Find<TextBox>("UrlInput")!, "https://git.example/owner/project");
        await Click(app.Window, installer.Find<RadioButton>("SourceChoice")!);
        await Click(app.Window, installer.Find<PclButton>("LoadButton")!);
        await Wait(() => !installer.IsBusy, "Branches did not load");
        await Click(app.Window, installer.Find<ComboBox>("BranchInput")!);
        app.Window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None); app.Window.KeyReleaseQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
        app.Window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None); app.Window.KeyReleaseQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        await Settle();
        app.Screenshot("extensions-url-source");
        await Click(app.Window, installer.Find<PclButton>("ConfirmInstallButton")!);
        await Wait(() => !installer.IsBusy, "Source update did not finish");
        Assert.AreEqual("2.0.0", app.Session.Host.Get("example.hello").Manifest.Version, installer.Find<TextBlock>("InstallErrorText")!.Text);
        Assert.AreEqual("develop", builtBranch);
    });

    [TestMethod]
    public Task ToolsNavigation_RepeatedToggles_PetSettings_LocaleAndRestart() => Run(async () =>
    {
        await using var app = new DesktopFixture();
        await app.Start();
        var toolbar = app.Window.Find<StackPanel>("ExtensionToolbar")!;
        Assert.IsFalse(toolbar.Children.OfType<PclIconButton>().Any(button => (string?)ToolTip.GetTip(button) == "Extensions"), "The extension manager must not add a toolbar button.");
        await Click(app.Window, app.Window.GetVisualDescendants().OfType<PclTabButton>().Single(tab => tab.IsEffectivelyVisible && tab.Text == "Settings"));
        Assert.IsFalse(app.Launcher.SidebarSections.SelectMany(section => section.Items).Any(item => item.Title == "Extensions"));
        await app.OpenExtensions();
        Assert.AreEqual("Tools/ToolsExtensions", app.Launcher.ExtensionRoute);
        Assert.AreEqual(2, app.Window.GetVisualDescendants().OfType<ExtensionCardView>().Count());
        CollectionAssert.AreEquivalent(new[] { "pcl.desktop-pet", "pcl.global-search" }, app.Session.Host.Extensions.Select(info => info.Manifest.Id).ToArray());
        app.Screenshot("extensions-settings");
        foreach (var id in new[] { "pcl.desktop-pet", "pcl.global-search" })
        {
            // The desktop pet is disabled by default; normalize to enabled so the
            // toggle cycle below always starts from a known state.
            if (!app.Session.Host.Get(id).IsEnabled) await app.Toggle(id);
            for (var i = 0; i < 3; i++)
            {
                await app.Toggle(id);
                Assert.AreEqual(ExtensionStatus.Disabled, app.Session.Host.Get(id).Status);
                await app.Toggle(id);
                Assert.AreEqual(ExtensionStatus.Active, app.Session.Host.Get(id).Status);
            }
        }
        await app.Expand("pcl.desktop-pet");
        var card = app.Card("pcl.desktop-pet");
        var preset = card.GetVisualDescendants().OfType<ComboBox>().First();
        await Click(app.Window, preset);
        app.Window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None); app.Window.KeyReleaseQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
        app.Window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None); app.Window.KeyReleaseQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        await Settle();
        var expectedPreset = app.Session.Host.Get("pcl.desktop-pet").Settings.Get("preset", "creeper_gif");
        Assert.AreNotEqual("creeper_gif", expectedPreset);
        await app.Toggle("pcl.global-search");
        Assert.AreSame(preset, card.GetVisualDescendants().OfType<ComboBox>().First(), "Other extensions must not recreate active editors.");
        app.Text.SetLocale("zh-Hans"); await Settle();
        Assert.AreEqual("PCL-ME 桌宠", card.Find<PclCard>("Card")!.Header);
        app.Screenshot("extensions-settings-zh");
        await app.Restart(); await app.OpenExtensions();
        Assert.AreEqual(expectedPreset, app.Session.Host.Get("pcl.desktop-pet").Settings.Get("preset", ""));
        Assert.AreEqual(ExtensionStatus.Disabled, app.Session.Host.Get("pcl.global-search").Status);
    });

    [TestMethod]
    public Task ExternalPage_SearchNavigation_DisableCleanup_AndRestartPersistence() => Run(async () =>
    {
        await using var app = new DesktopFixture();
        await app.Start(); await app.OpenExtensions();
        await Install(app, CreatePackage(app, "1.0.0"));
        await app.Toggle("example.hello");
        var actions = app.Window.GetVisualDescendants().OfType<ExtensionManagerView>().Single().Find<PclButton>("ActionsButton")!;
        await Click(app.Window, actions);
        var menu = (MenuFlyout)actions.ContextFlyout!;
        await Wait(() => menu.IsOpen, "Extension actions did not open from the Tools page.");
        var open = menu.Items.OfType<MenuItem>().Single(item => (string?)item.Header == "Hello extension");
        await Click(TopLevel.GetTopLevel(open)!, open);
        await Wait(() => app.Window.Find<ContentControl>("ExtensionWorkspace")!.IsVisible, "The relocated menu did not open the installed extension page.");
        var workspace = app.Window.Find<ContentControl>("ExtensionWorkspace")!;
        Assert.IsTrue(workspace.GetVisualDescendants().OfType<PclCard>().Any(card => card.Header == "Hello extension"),
            "An installed extension must render its native page using the shared PCL controls and its own localization.");
        await app.Session.NavigateAsync("Setup/SetupUI");
        await app.Session.NavigateAsync("Setup/SetupExtensions");
        Assert.AreEqual("Tools/ToolsExtensions", app.Launcher.ExtensionRoute, "Existing extension links must redirect to the new manager route.");
        await Wait(() => app.Window.GetVisualDescendants().OfType<ExtensionManagerView>().Any(), "The legacy management link did not open Tools → Extensions.");
        await app.Search("Extensions");
        app.Window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        app.Window.KeyReleaseQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        await Wait(() => app.Window.GetVisualDescendants().OfType<ExtensionManagerView>().Any(), "Search did not navigate to extension settings");
        await app.Toggle("example.hello");
        Assert.AreEqual(ExtensionStatus.Disabled, app.Session.Host.Get("example.hello").Status);
        await app.Restart(); await app.OpenExtensions();
        Assert.AreEqual(ExtensionStatus.Disabled, app.Session.Host.Get("example.hello").Status);
        await app.Toggle("example.hello");
        await OpenPage();
        await app.Session.Host.SetEnabledAsync("example.hello", false);
        await Settle();
        Assert.IsFalse(app.Window.Find<ContentControl>("ExtensionWorkspace")!.IsVisible);
        Assert.IsFalse((await app.Session.Host.QueryAsync(new("Hello"))).Any(result => result.CommandId == "example.hello.open"));

        async Task OpenPage()
        {
            await app.Search("Hello");
            app.Window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            app.Window.KeyReleaseQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            await Wait(() => app.Window.Find<ContentControl>("ExtensionWorkspace")!.IsVisible, "Search did not open the installed extension page");
        }
    });

    [TestMethod]
    public Task PackagePicker_Cancel_InvalidArchive_Install_Enable_Update_Restart_Uninstall() => Run(async () =>
    {
        await using var app = new DesktopFixture();
        await app.Start(); await app.OpenExtensions();
        var manager = app.Window.GetVisualDescendants().OfType<ExtensionManagerView>().Single();
        await Click(app.Window, manager.Find<PclButton>("InstallButton")!);
        Assert.IsNotNull(app.Picker.Dialog);
        await Click(app.Picker.Dialog, app.Picker.Dialog.Find<Button>("PickerCancel")!);
        Assert.AreEqual(2, app.Session.Host.Extensions.Count);
        var invalid = Path.Combine(app.Root, "invalid.pclext"); File.WriteAllText(invalid, "Invalid ZIP");
        await Install(app, invalid);
        Assert.IsTrue(manager.Find<TextBlock>("FeedbackText")!.IsVisible);
        var package = CreatePackage(app, "1.0.0");
        await Install(app, package);
        Assert.IsFalse(manager.Find<TextBlock>("FeedbackText")!.IsVisible);
        Assert.AreEqual(ExtensionStatus.Disabled, app.Session.Host.Get("example.hello").Status);
        await app.Toggle("example.hello");
        Assert.AreEqual(ExtensionStatus.Active, app.Session.Host.Get("example.hello").Status);
        var updateButton = app.Card("example.hello").Find<PclButton>("UpdateButton")!;
        await Click(app.Window, updateButton);
        var updateMenu = (MenuFlyout)updateButton.ContextFlyout!;
        var updateFromFile = updateMenu.Items.OfType<MenuItem>().First();
        await Click(TopLevel.GetTopLevel(updateFromFile)!, updateFromFile);
        var updateDialog = app.Picker.Dialog!;
        await Type(updateDialog, updateDialog.Find<TextBox>("PickerPath")!, CreatePackage(app, "2.0.0"));
        await Click(updateDialog, updateDialog.Find<Button>("PickerOpen")!);
        await Wait(() => app.Session.Host.Get("example.hello").RestartRequired, "Card update did not finish");
        Assert.IsTrue(app.Session.Host.Get("example.hello").RestartRequired);
        await app.Restart(); await app.OpenExtensions(); await app.Expand("example.hello");
        Assert.AreEqual("2.0.0", app.Session.Host.Get("example.hello").Manifest.Version);
        await Click(app.Window, app.Card("example.hello").Find<PclButton>("UninstallButton")!);
        Assert.IsTrue(app.Session.Host.Get("example.hello").RemovalPending);
        await app.Restart(); await app.OpenExtensions();
        Assert.IsFalse(app.Session.Host.Extensions.Any(info => info.Manifest.Id == "example.hello"));
    });

    private static async Task Install(DesktopFixture app, string package)
    {
        var manager = app.Window.GetVisualDescendants().OfType<ExtensionManagerView>().Single();
        await Click(app.Window, manager.Find<PclButton>("InstallButton")!);
        var dialog = app.Picker.Dialog!;
        await Type(dialog, dialog.Find<TextBox>("PickerPath")!, package);
        await Click(dialog, dialog.Find<Button>("PickerOpen")!);
        await Wait(() => manager.Find<PclButton>("InstallButton")!.IsEnabled, "Package operation did not finish");
    }

    [TestMethod]
    public Task Shortcuts_ConflictValidation_KeyboardDispatch_AndSafeModePreferences() => Run(async () =>
    {
        await using var app = new DesktopFixture();
        await app.Start(); await app.OpenExtensions();
        // The desktop pet is disabled by default; the shortcut editor needs it active.
        if (!app.Session.Host.Get("pcl.desktop-pet").IsEnabled) await app.Toggle("pcl.desktop-pet");
        await app.Expand("pcl.desktop-pet");
        var pet = app.Card("pcl.desktop-pet");
        const string fieldName = "Shortcut_pcl_desktop_pet_hide";
        const string saveName = "SaveShortcut_pcl_desktop_pet_hide";
        await Type(app.Window, pet.Find<TextBox>(fieldName)!, "Primary+Shift+P");
        await Click(app.Window, pet.Find<PclButton>(saveName)!);
        Assert.IsTrue(pet.Find<TextBlock>("OperationErrorText")!.IsVisible);
        Assert.IsNull(app.Session.Host.Commands.Single(command => command.Definition.Id == "pcl.desktop-pet.hide").Definition.Shortcut);
        await Type(app.Window, pet.Find<TextBox>(fieldName)!, "Primary+Alt+H");
        await Click(app.Window, pet.Find<PclButton>(saveName)!);
        Assert.IsFalse(pet.Find<TextBlock>("OperationErrorText")!.IsVisible, pet.Find<TextBlock>("OperationErrorText")!.Text);
        app.Window.KeyPressQwerty(PhysicalKey.H, (OperatingSystem.IsMacOS() ? RawInputModifiers.Meta : RawInputModifiers.Control) | RawInputModifiers.Alt);
        app.Window.KeyReleaseQwerty(PhysicalKey.H, RawInputModifiers.None);
        await Wait(() => app.Session.Host.Get("pcl.desktop-pet").Status == ExtensionStatus.Disabled, "Updated shortcut did not execute");
        await app.Restart(safeMode: true); await app.OpenExtensions();
        Assert.IsTrue(app.Session.Host.SafeMode);
        Assert.AreEqual(0, app.Session.Windows.Count);
        Assert.IsTrue(app.Window.GetVisualDescendants().OfType<ExtensionManagerView>().Single().Find<TextBlock>("SafeModeText")!.IsVisible);
        await app.Toggle("pcl.desktop-pet");
        Assert.AreEqual(ExtensionStatus.Inactive, app.Session.Host.Get("pcl.desktop-pet").Status);
        Assert.AreEqual(0, app.Session.Windows.Count);
        await app.Restart(); await app.OpenExtensions(); await app.Expand("pcl.desktop-pet");
        Assert.AreEqual(ExtensionStatus.Active, app.Session.Host.Get("pcl.desktop-pet").Status);
        Assert.AreEqual("Primary+Alt+H", app.Card("pcl.desktop-pet").Find<TextBox>(fieldName)!.Text);
    });

    [TestMethod]
    public Task SettingsEditors_Reset_DarkTheme_AllLocales_AndReturnNavigation() => Run(async () =>
    {
        await using var app = new DesktopFixture();
        await app.Start(); await app.OpenExtensions(); await app.Expand("pcl.global-search");
        var card = app.Card("pcl.global-search");
        var limit = card.Find<NumericUpDown>("Setting_limit")!;
        await Type(app.Window, limit.GetVisualDescendants().OfType<TextBox>().Single(), "12");
        await Click(app.Window, card.Find<PclButton>("ResetButton")!);
        Assert.AreEqual(30, app.Session.Host.Get("pcl.global-search").Settings.Get("limit", 0d));
        app.Actions.PersistSharedValue("UiDarkMode", 1);
        Workflows.FrontendAppearanceService.ApplyStoredAppearance(global::Avalonia.Application.Current!, app.Paths);
        Assert.AreEqual(global::Avalonia.Styling.ThemeVariant.Dark, app.Window.ActualThemeVariant);
        app.Window.Width = 810; app.Window.Height = 470;
        foreach (var locale in new[] { "en-US", "zh-Hans", "zh-Hant", "lzh", "de-DE", "fr-FR", "es-ES", "ko-KR" })
        {
            app.Text.SetLocale(locale); await Settle();
            Assert.AreSame(card, app.Card("pcl.global-search"));
            var title = card.Find<PclCard>("Card")!.FindControl<TextBlock>("HeaderTextBlock")!;
            var actions = card.Find<StackPanel>("HeaderActions")!;
            Assert.IsTrue(title.Bounds.Right <= actions.TranslatePoint(default, title.GetVisualParent()!)!.Value.X,
                "Extension title overlaps its actions in " + locale);
            var manager = app.Window.GetVisualDescendants().OfType<ExtensionManagerView>().Single();
            var install = manager.Find<PclButton>("InstallUrlButton")!;
            var installCorner = install.TranslatePoint(new Point(install.Bounds.Width, install.Bounds.Height), manager)!.Value;
            Assert.IsTrue(installCorner.X <= manager.Bounds.Width - 20, "Install actions overflow in " + locale);
            var search = manager.Find<PclSearchBox>("ExtensionSearch")!;
            Assert.IsTrue(installCorner.Y < search.TranslatePoint(default, manager)!.Value.Y, "Install actions overlap search in " + locale);
        }
        app.Text.SetLocale("en-US"); await Task.Delay(500); Tick();
        card.BringIntoView(); await Settle(); app.Screenshot("extensions-dark");
        await Click(app.Window, card.Find<PclCard>("Card")!.FindControl<Button>("HeaderButton")!);
        app.Window.GetVisualDescendants().OfType<ExtensionManagerView>().Single().Find<TextBlock>("TitleText")!.BringIntoView();
        await Settle(); app.Screenshot("extensions-dark-overview");
        await Click(app.Window, app.Window.GetVisualDescendants().OfType<PclListItem>().Single(item => item.Title == "Tests"));
        await Wait(() => !app.Window.GetVisualDescendants().OfType<ExtensionManagerView>().Any(), "Leaving the Extensions tool did not release its view.");
        await app.OpenExtensions();
        Assert.IsFalse(app.Card("pcl.global-search").Find<PclCard>("Card")!.IsChevronExpanded);
    });

    [TestMethod]
    public Task InstanceSearch_NativeTab_AndDisableCleanup() => Run(async () =>
    {
        await using var app = new DesktopFixture();
        var instance = app.CreateInstance("E2E-example");
        await app.Start(); await app.OpenExtensions();
        await Install(app, CreatePackage(app, "1.0.0")); await app.Toggle("example.hello");
        await app.Search("E2E-example");
        var search = app.Window;
        search.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        if (search.IsVisible) search.KeyReleaseQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        await Wait(() => app.Window.GetVisualDescendants().OfType<TabItem>().Any(tab => (string?)tab.Header == "Extension information"), "Instance search did not open the extension tab");
        var tab = app.Window.GetVisualDescendants().OfType<TabItem>().Single(tab => (string?)tab.Header == "Extension information");
        Assert.AreEqual(instance, ((TextBlock)tab.Content!).Text);
        await app.OpenExtensions(); await app.Toggle("example.hello");
        Assert.IsNull(app.Launcher.ExtensionInstanceTabsContent);
    });

    private static string CreatePackage(DesktopFixture app, string version, string? website = null, string? authorWebsite = null, string author = "")
    {
        var path = Path.Combine(app.Root, "hello-" + version + ".pclext");
        var manifest = new ExtensionManifest
        {
            Id = "example.hello", Name = "Hello extension", Version = version, AvaloniaMajorVersion = 11, StartWithLauncher = true,
            Website = website, AuthorWebsite = authorWebsite, Author = author,
            EntryAssembly = "HelloExtension.dll", EntryType = "HelloExtension.HelloExtension",
            Commands = [new() { Id = "example.hello.open", Title = "Hello" }]
        };
        using var zip = ZipFile.Open(path, ZipArchiveMode.Create);
        using (var stream = zip.CreateEntry("extension.json").Open()) JsonSerializer.Serialize(stream, manifest, ExtensionPackage.JsonOptions);
        zip.CreateEntryFromFile(typeof(HelloExtension.HelloExtension).Assembly.Location, "HelloExtension.dll");
        foreach (var locale in Directory.EnumerateFiles(Path.Combine(AppContext.BaseDirectory, "Locales"), "*.json"))
            zip.CreateEntryFromFile(locale, "Locales/" + Path.GetFileName(locale));
        return path;
    }
}
