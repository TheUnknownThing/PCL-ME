using System.Net;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Input.Raw;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.Minecraft.Launch;
using PCL.Frontend.Avalonia.Desktop.Controls;
using PCL.Frontend.Avalonia.Workflows;
using static PCL.Frontend.Avalonia.E2E.DesktopFixture;

namespace PCL.Frontend.Avalonia.E2E;

[TestClass]
[DoNotParallelize]
public sealed class LoginPopupE2E
{
    private const string MicrosoftProfileId = "abcdef0123456789abcdef0123456789";

    [TestMethod]
    [DataRow("en-US", false)]
    [DataRow("en-US", true)]
    [DataRow("zh-Hans", true)]
    [DataRow("de-DE", false)]
    public Task ThirdPartyLogin_SeparatesProviderAndCredentials_AndPreservesCustomServer(string locale, bool dark) => Run(async () =>
    {
        await using var app = new DesktopFixture();
        SaveOfflineProfile(app);
        await app.Start();
        app.Text.SetLocale(locale);
        app.Actions.PersistSharedValue("UiDarkMode", dark ? 1 : 0);
        FrontendAppearanceService.ApplyStoredAppearance(Application.Current!, app.Paths);
        app.Launcher.LoginAuthlibLaunchProfileCommand.Execute(null);
        await WaitForPopup(app);
        app.Launcher.LaunchProfileHttpClientFactory = () => throw new AssertFailedException("Incomplete sign-in forms must not make network requests.");
        var littleSkin = app.Window.Find<RadioButton>("LittleSkinProvider")!;
        var custom = app.Window.Find<RadioButton>("CustomAuthlibProvider")!;
        var serverFields = app.Window.Find<StackPanel>("CustomAuthlibServerFields")!;
        var userName = app.Window.Find<TextBox>("AuthlibUserName")!;
        var password = app.Window.Find<TextBox>("AuthlibPassword")!;
        Assert.IsTrue(littleSkin.IsChecked == true);
        Assert.IsFalse(serverFields.IsEffectivelyVisible, "LittleSkin must not require editing an API URL.");
        Assert.IsTrue(userName.IsFocused, "LittleSkin sign-in should start at the account field.");
        app.Screenshot($"login-third-party-{locale}-{(dark ? "dark" : "light")}");

        await Click(app.Window, custom);
        Assert.IsTrue(serverFields.IsEffectivelyVisible);
        var serverUrl = app.Window.Find<TextBox>("AuthlibServerUrl")!;
        Assert.AreEqual(string.Empty, serverUrl.Text, "Custom servers need their own authentication URL.");
        await Click(app.Window, app.Window.Find<PclButton>("AuthlibSignInButton")!);
        await Wait(() => app.Launcher.HasLaunchAuthlibStatus, "An empty form must show an inline error.");
        Assert.AreEqual(app.Text.T("launch.profile.authlib.errors.required_fields"), app.Launcher.LaunchAuthlibStatusText);
        const string customUrl = "https://auth.example.invalid/api/yggdrasil";
        await Type(app.Window, serverUrl, customUrl);
        await Type(app.Window, userName, "player@example.invalid");
        await Type(app.Window, password, "test-password");
        Assert.AreEqual('●', password.PasswordChar);
        await Click(app.Window, littleSkin);
        Assert.AreEqual("https://littleskin.cn/api/yggdrasil", app.Launcher.LaunchAuthlibServer);
        Assert.IsFalse(serverFields.IsEffectivelyVisible);
        await Click(app.Window, custom);
        Assert.AreEqual(customUrl, serverUrl.Text, "Switching providers must preserve the custom server address.");
        Assert.AreEqual("player@example.invalid", userName.Text);
        Assert.AreEqual("test-password", password.Text);
        if (locale == "en-US")
        {
            var scroll = password.GetVisualAncestors().OfType<ScrollViewer>().First();
            Assert.IsTrue(scroll.Extent.Height <= scroll.Viewport.Height + 1,
                "All custom-server fields must fit at the default window size.");
        }
        app.Screenshot($"login-third-party-custom-{locale}-{(dark ? "dark" : "light")}");

        // A server loaded from instance/profile settings must select its provider automatically.
        app.Launcher.LaunchAuthlibServer = "https://another.example.invalid/authserver";
        await Settle();
        Assert.IsTrue(custom.IsChecked == true);
        Assert.AreEqual(app.Launcher.LaunchAuthlibServer, serverUrl.Text);
        app.Window.Height = app.Window.MinHeight;
        await Settle();
        await Type(app.Window, password, string.Empty);
        app.Window.KeyPressQwerty(PhysicalKey.Backspace, RawInputModifiers.None);
        app.Window.KeyReleaseQwerty(PhysicalKey.Backspace, RawInputModifiers.None);
        await Settle();
        Assert.AreEqual(string.Empty, app.Launcher.LaunchAuthlibPassword);
        await Click(app.Window, app.Window.Find<PclButton>("AuthlibSignInButton")!);
        await Wait(() => app.Launcher.HasLaunchAuthlibStatus, "Missing credentials must show an inline error.");
        Assert.AreEqual(app.Text.T("launch.profile.authlib.errors.required_fields"), app.Launcher.LaunchAuthlibStatusText);
        Assert.IsTrue(app.Launcher.IsLaunchProfilePopupVisible);
        var close = app.Window.Find<PclButton>("CloseLoginPopupButton")!;
        var card = app.Window.Find<Border>("LoginCard")!;
        Assert.IsTrue(card.Bounds.Height <= app.Window.Bounds.Height - 56, "The form must fit a short launcher window.");
        var status = app.Window.Find<TextBlock>("AuthlibLoginStatus")!;
        app.Screenshot($"login-third-party-compact-{locale}-{(dark ? "dark" : "light")}");
        // Hit the first text line: short translations leave empty space to the right.
        var statusPoint = status.TranslatePoint(new Point(4, Math.Min(7, status.Bounds.Height / 2)), app.Window)!.Value;
        Assert.AreSame(status, app.Window.InputHitTest(statusPoint), "Validation errors must remain visible above the actions in a short window.");
        await Click(app.Window, close);
        await Wait(() => !app.Launcher.IsLaunchProfilePopupVisible, "Cancel did not close third-party sign-in.");
        Assert.AreEqual(1, FrontendProfileStorageService.Load(app.Paths).Document.Profiles.Count);
    });

    [TestMethod]
    public Task LoginPopup_ContainsAllForms_BlocksTheSidebar_AndKeepsKeyboardFocus() => Run(async () =>
    {
        await using var app = new DesktopFixture();
        SaveOfflineProfile(app);
        await app.Start();
        var avatar = app.Window.Find<Image>("AccountAvatar")!;
        var avatarOrigin = avatar.TranslatePoint(default, app.Window);
        var lifetime = (IClassicDesktopStyleApplicationLifetime)Application.Current!.ApplicationLifetime!;
        var windowCount = lifetime.Windows.Count;
        app.Launcher.AddLaunchProfileCommand.Execute(null);
        await WaitForPopup(app);
        var overlay = app.Window.Find<LaunchProfileLoginOverlay>("LoginOverlay")!;
        Assert.IsTrue(app.Launcher.ShowLaunchProfileChooser);
        Assert.IsTrue(overlay.IsKeyboardFocusWithin);
        Assert.IsFalse(app.Launcher.IsTopLevelNavigationInteractive);
        Assert.AreEqual(avatarOrigin, avatar.TranslatePoint(default, app.Window));
        Assert.AreEqual(windowCount, lifetime.Windows.Count, "Login must stay inside the launcher window.");
        Assert.AreSame(app.Window, TopLevel.GetTopLevel(overlay));
        var avatarPoint = avatar.TranslatePoint(new Point(36, 36), app.Window)!.Value;
        Assert.AreEqual("LoginBackdrop", (app.Window.InputHitTest(avatarPoint) as Control)?.Name);
        for (var i = 0; i < 18; i++)
        {
            app.Window.KeyPressQwerty(PhysicalKey.Tab, RawInputModifiers.None);
            app.Window.KeyReleaseQwerty(PhysicalKey.Tab, RawInputModifiers.None);
            Assert.IsTrue(overlay.IsKeyboardFocusWithin, "Tab navigation must stay inside the popup.");
        }
        await Settle();
        app.Screenshot("login-popup-choose-method");
        app.Actions.PersistSharedValue("UiDarkMode", 1);
        FrontendAppearanceService.ApplyStoredAppearance(Application.Current!, app.Paths);
        await Settle();
        app.Screenshot("login-popup-choose-method-dark");
        await Click(app.Window, app.Window.Find<Button>("OfflineLoginMethod")!);
        await Settle();
        Assert.IsTrue(overlay.Find<Control>("OfflineEditor")!.IsEffectivelyVisible);
        Assert.IsTrue(overlay.Find<TextBox>("OfflineUserName")!.IsFocused);
        app.Screenshot("login-popup-offline");
        app.Launcher.AddLaunchProfileCommand.Execute(null);
        await Settle();
        await Click(app.Window, app.Window.Find<Button>("ThirdPartyLoginMethod")!);
        Assert.IsTrue(overlay.Find<Control>("AuthlibEditor")!.IsEffectivelyVisible);
        Assert.IsFalse(app.Window.GetVisualDescendants().OfType<PclLaunchLeftPanel>().Single().GetVisualDescendants().OfType<TextBox>().Any(),
            "The sidebar must no longer contain account forms.");
        app.Screenshot("login-popup-third-party");
        app.Window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        app.Window.KeyReleaseQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        await Wait(() => !overlay.Find<Grid>("OverlayRoot")!.IsVisible, "Escape did not close the popup.");
        Assert.IsTrue(app.Launcher.ShowLaunchProfileSummaryCard);
        Assert.IsTrue(app.Launcher.IsTopLevelNavigationInteractive);
    });

    [TestMethod]
    [DataRow("success", "ABCDEFGH", false)]
    [DataRow("success", "ABCD-EFGH", true)]
    [DataRow("expired_token", "ABCDEFGH", false)]
    [DataRow("authorization_declined", "ABCDEFGH", false)]
    [DataRow("no-ownership", "ABCDEFGH", false)]
    public Task MicrosoftLogin_CopiesCode_PollsAutomatically_AndAllowsRetry(string outcome, string userCode, bool keyboard) => Run(async () =>
    {
        using var clientId = new ClientIdScope();
        await using var app = new DesktopFixture();
        SaveOfflineProfile(app);
        await app.Start();
        using var handler = new MicrosoftHandler { Outcome = outcome, UserCode = userCode };
        ConfigureMicrosoft(app, handler);
        app.Launcher.AddLaunchProfileCommand.Execute(null);
        await WaitForPopup(app);
        await Click(app.Window, app.Window.Find<Button>("MicrosoftLoginMethod")!);
        await Wait(() => handler.PollCount > 0, "The initial click must automatically start polling.");
        Assert.IsTrue(app.Launcher.IsLaunchMicrosoftLoginInProgress);
        Assert.IsTrue(app.Launcher.ShowLaunchMicrosoftDeviceCode);
        Assert.IsFalse(app.Window.Find<TextBlock>("MicrosoftLoginStatus")!.IsEffectivelyVisible,
            "The browser prompt must not duplicate the waiting status.");
        Assert.IsFalse(app.Window.Find<ProgressBar>("MicrosoftLoginProgress")!.IsEffectivelyVisible,
            "Waiting in the browser needs only the code and its prompt.");
        Assert.IsTrue(app.Window.Find<Border>("LoginCard")!.Bounds.Height < 300,
            "The login dialog must size to its content.");
        Assert.AreEqual("ABCDEFGH", await app.Window.Clipboard!.TryGetTextAsync());
        Assert.IsFalse(app.Launcher.IsPromptOverlayVisible, "Automatic copying must not interrupt sign-in with an alert.");
        var code = app.Window.Find<TextBlock>("MicrosoftDeviceCode")!;
        Assert.AreEqual("ABCD-EFGH", code.Text, "The separator must be present only in the displayed code.");
        var copyCode = app.Window.Find<Button>("MicrosoftDeviceCodeButton")!;
        Assert.IsFalse(copyCode.GetVisualDescendants().OfType<SelectableTextBlock>().Any(), "The code and its decorative separator must not be selectable.");
        Assert.IsNull(ToolTip.GetTip(copyCode), "The code must not show a hover tooltip.");
        app.Window.MouseMove(code.TranslatePoint(new Point(code.Bounds.Width / 2, code.Bounds.Height / 2), app.Window)!.Value);
        await Settle();
        Assert.AreEqual(0, code.TextDecorations?.Count ?? 0, "Hovering the code must not underline it.");
        Assert.IsNull(app.Window.Find<PclButton>("CopyMicrosoftCodeButton"), "Copying must be offered through the code itself.");
        Assert.IsTrue(app.Launcher.BackLaunchProfileCommand.CanExecute(null), "Waiting for authorization must be cancellable.");
        Assert.IsTrue(app.Launcher.OpenMicrosoftDeviceLinkCommand.CanExecute(null), "Browser retry must remain available while polling.");
        Assert.IsFalse(app.Window.Find<PclButton>("MicrosoftSignInButton")!.IsVisible, "Waiting must not require another Continue click.");
        app.Screenshot("login-popup-microsoft-code");
        if (outcome == "success")
        {
            app.Actions.PersistSharedValue("UiDarkMode", 1);
            FrontendAppearanceService.ApplyStoredAppearance(Application.Current!, app.Paths);
            await Settle();
            app.Screenshot("login-popup-microsoft-dark");
        }
        await app.Window.Clipboard!.SetTextAsync("replaced clipboard");
        var lifetime = (IClassicDesktopStyleApplicationLifetime)Application.Current!.ApplicationLifetime!;
        var windowCount = lifetime.Windows.Count;
        if (keyboard)
        {
            copyCode.Focus();
            app.Window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            app.Window.KeyReleaseQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            await Settle();
        }
        else await Click(app.Window, code);
        Assert.AreEqual("ABCDEFGH", await app.Window.Clipboard!.TryGetTextAsync(), "The decorative hyphen must never be copied.");
        var hints = app.Window.Find<StackPanel>("PanHint")!;
        await Wait(() => hints.GetVisualDescendants().OfType<TextBlock>().Any(text => text.IsEffectivelyVisible
            && text.Text == app.Text.T("launch.profile.microsoft.messages.code_copied")), "Copying the code must show a bottom-right notification.");
        Assert.IsFalse(app.Launcher.IsPromptOverlayVisible, "Copy feedback must not open a modal popup.");
        Assert.IsTrue(app.Launcher.IsLaunchProfilePopupVisible, "The login dialog must remain usable while copy feedback is visible.");
        var loginOverlay = app.Window.Find<LaunchProfileLoginOverlay>("LoginOverlay")!;
        Assert.AreSame(loginOverlay.Parent, hints.Parent);
        Assert.IsTrue(hints.ZIndex > loginOverlay.ZIndex, "Copy notifications must appear above the login backdrop without dimming.");
        Assert.AreEqual(windowCount, lifetime.Windows.Count, "The notification must stay inside the launcher window.");
        Assert.AreEqual(0, code.TextDecorations?.Count ?? 0, "Activating the code must not underline it.");
        app.Screenshot("login-popup-microsoft-code-copied");
        handler.Authorized = true;
        await Wait(() => !app.Launcher.IsLaunchMicrosoftLoginInProgress, "Microsoft login did not finish automatically.");

        if (outcome != "success")
        {
            Assert.IsTrue(app.Launcher.IsLaunchProfilePopupVisible, "Errors must stay in the popup for retry.");
            Assert.IsTrue(app.Launcher.CanStartLaunchMicrosoftLogin);
            Assert.IsFalse(app.Launcher.HasLaunchMicrosoftDeviceCode, "A rejected code must not be reused.");
            Assert.IsFalse(string.IsNullOrWhiteSpace(app.Launcher.LaunchMicrosoftStatusText));
            Assert.AreEqual(1, FrontendProfileStorageService.Load(app.Paths).Document.Profiles.Count);
            handler.Outcome = "success";
            await Click(app.Window, app.Window.Find<PclButton>("MicrosoftSignInButton")!);
            await Wait(() => !app.Launcher.IsLaunchProfilePopupVisible, "Retry did not finish automatically.");
            Assert.AreEqual(2, handler.DeviceCodeCount, "Retry must request a fresh code.");
        }

        await Wait(() => !app.Window.Find<LaunchProfileLoginOverlay>("LoginOverlay")!.Find<Grid>("OverlayRoot")!.IsVisible,
            "The popup did not close after successful login.");
        var document = FrontendProfileStorageService.Load(app.Paths).Document;
        Assert.AreEqual(2, document.Profiles.Count);
        Assert.AreEqual(1, document.LastUsedProfile);
        Assert.AreEqual(MicrosoftProfileId, document.Profiles[1].Uuid);
        Assert.AreEqual("SignedInPlayer", app.Launcher.LaunchUserName);
        Assert.AreEqual("minecraft-token", document.Profiles[1].AccessToken);
        Assert.AreEqual("refresh-token", document.Profiles[1].RefreshToken);
        Assert.IsTrue(app.Launcher.ShowLaunchProfileSummaryCard);
    });

    [TestMethod]
    [DataRow("device-code")]
    [DataRow("token")]
    [DataRow("xbox")]
    [DataRow("profile")]
    [DataRow("poll-delay")]
    public Task MicrosoftLogin_CancelStopsRequestsWithoutSavingAnAccount(string stage) => Run(async () =>
    {
        using var clientId = new ClientIdScope();
        await using var app = new DesktopFixture();
        SaveOfflineProfile(app);
        await app.Start();
        using var handler = new MicrosoftHandler { BlockStage = stage == "poll-delay" ? null : stage, Authorized = stage != "poll-delay" };
        ConfigureMicrosoft(app, handler);
        app.Launcher.LoginMicrosoftLaunchProfileCommand.Execute(null);
        await WaitForPopup(app);
        await Wait(() => stage == "poll-delay" ? handler.PollCount > 0 : handler.Blocked, "The request did not reach " + stage);
        await Click(app.Window, app.Window.Find<PclButton>("CloseLoginPopupButton")!);
        await Wait(() => !app.Launcher.IsLaunchMicrosoftLoginInProgress, "Cancellation did not interrupt the request.");
        Assert.AreEqual(stage != "poll-delay", handler.Canceled);
        Assert.IsFalse(app.Launcher.IsLaunchProfilePopupVisible);
        Assert.IsFalse(app.Launcher.HasLaunchMicrosoftDeviceCode);
        Assert.IsTrue(app.Launcher.AddLaunchProfileCommand.CanExecute(null));
        Assert.AreEqual(1, FrontendProfileStorageService.Load(app.Paths).Document.Profiles.Count);
        // Reopening after cancellation must start a clean session.
        handler.BlockStage = null;
        handler.Authorized = true;
        app.Launcher.LoginMicrosoftLaunchProfileCommand.Execute(null);
        await Wait(() => !app.Launcher.IsLaunchProfilePopupVisible, "A canceled login prevented a new attempt.");
        Assert.AreEqual(2, FrontendProfileStorageService.Load(app.Paths).Document.Profiles.Count);
    });

    [TestMethod]
    public Task MicrosoftLogin_ExpiryInterruptsAnOutstandingPoll_AndOffersRetry() => Run(async () =>
    {
        using var clientId = new ClientIdScope();
        await using var app = new DesktopFixture();
        await app.Start();
        using var handler = new MicrosoftHandler { BlockStage = "token", ExpiresInSeconds = 1 };
        ConfigureMicrosoft(app, handler);
        app.Launcher.LoginMicrosoftLaunchProfileCommand.Execute(null);
        await Wait(() => handler.Blocked, "The token poll did not start.");
        await Wait(() => !app.Launcher.IsLaunchMicrosoftLoginInProgress, "Expiry did not interrupt the pending request.");
        Assert.IsTrue(handler.Canceled);
        Assert.IsTrue(app.Launcher.IsLaunchProfilePopupVisible);
        Assert.IsTrue(app.Launcher.CanStartLaunchMicrosoftLogin);
        Assert.IsFalse(app.Launcher.HasLaunchMicrosoftDeviceCode);
        Assert.AreEqual(app.Text.T("launch.profile.microsoft.errors.authorization_timeout"), app.Launcher.LaunchMicrosoftStatusText);
        Assert.AreEqual(0, FrontendProfileStorageService.Load(app.Paths).Document.Profiles.Count);
        await WaitForPopup(app);
        app.Screenshot("login-popup-microsoft-expired");
    });

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public Task MicrosoftLogin_ReauthenticationPreservesTheSavedAccountIdentity(bool differentAccount) => Run(async () =>
    {
        using var clientId = new ClientIdScope();
        await using var app = new DesktopFixture();
        var document = FrontendProfileStorageService.CreateOfflineProfile(MinecraftLaunchProfileDocument.Empty,
            "SavedPlayer", differentAccount ? new string('a', 32) : MicrosoftProfileId);
        document = document with { Profiles = [document.Profiles[0] with
        {
            Kind = MinecraftLaunchStoredProfileKind.Microsoft, AccessToken = "old-token", RefreshToken = "old-refresh", Desc = "Keep this description"
        }] };
        FrontendProfileStorageService.Save(app.Paths, document);
        await app.Start();
        document = FrontendProfileStorageService.Load(app.Paths).Document;
        using var handler = new MicrosoftHandler();
        ConfigureMicrosoft(app, handler);
        app.Launcher.EditLaunchProfileCommand.Execute(null);
        await Wait(() => handler.PollCount > 0, "Editing a Microsoft account did not automatically start sign-in.");
        handler.Authorized = true;
        await Wait(() => !app.Launcher.IsLaunchMicrosoftLoginInProgress, "Reauthentication did not finish.");
        var saved = FrontendProfileStorageService.Load(app.Paths).Document;
        Assert.AreEqual(1, saved.Profiles.Count, "Reauthentication must update the existing entry.");
        Assert.AreEqual(document.Profiles[0].Uuid, saved.Profiles[0].Uuid);
        Assert.AreEqual("Keep this description", saved.Profiles[0].Desc);
        if (differentAccount)
        {
            Assert.AreEqual(document.Profiles[0], saved.Profiles[0]);
            Assert.IsTrue(app.Launcher.IsLaunchProfilePopupVisible);
            Assert.AreEqual(app.Text.T("launch.profile.edit.microsoft_account_changed"), app.Launcher.LaunchMicrosoftStatusText);
        }
        else
        {
            Assert.AreEqual("minecraft-token", saved.Profiles[0].AccessToken);
            Assert.IsFalse(app.Launcher.IsLaunchProfilePopupVisible);
        }
    });

    private static void SaveOfflineProfile(DesktopFixture app) => FrontendProfileStorageService.Save(app.Paths,
        FrontendProfileStorageService.CreateOfflineProfile(MinecraftLaunchProfileDocument.Empty,
            "ExistingPlayer", "0123456789abcdef0123456789abcdef"));

    private static async Task WaitForPopup(DesktopFixture app)
    {
        await Wait(() => app.Window.Find<Border>("LoginCard") is { IsEffectivelyVisible: true, Opacity: > .99 }, "Login popup did not open.");
        await Settle();
    }

    private static void ConfigureMicrosoft(DesktopFixture app, MicrosoftHandler handler)
    {
        app.Launcher.LaunchProfileHttpClientFactory = () => new HttpClient(handler, disposeHandler: false);
        app.Launcher.MicrosoftLoginBrowserOpener = url =>
        {
            Assert.IsTrue(Dispatcher.UIThread.CheckAccess(), "Clipboard and browser operations must stay on the UI thread.");
            Assert.AreEqual("https://example.invalid/device", url);
            // The headless clipboard completes synchronously, so this also verifies ordering.
            Assert.AreEqual("ABCDEFGH", app.Window.Clipboard!.TryGetTextAsync().GetAwaiter().GetResult(),
                "The code must already be copied when the browser opens.");
        };
    }

    private sealed class ClientIdScope : IDisposable
    {
        private readonly string? _previous = Environment.GetEnvironmentVariable("PCL_MS_CLIENT_ID");
        public ClientIdScope() => Environment.SetEnvironmentVariable("PCL_MS_CLIENT_ID", "test-client");
        public void Dispose() => Environment.SetEnvironmentVariable("PCL_MS_CLIENT_ID", _previous);
    }

    private sealed class MicrosoftHandler : HttpMessageHandler
    {
        public string Outcome { get; set; } = "success";
        public string UserCode { get; set; } = "ABCDEFGH";
        public bool Authorized { get; set; }
        public string? BlockStage { get; set; }
        public bool Blocked { get; private set; }
        public bool Canceled { get; private set; }
        public int PollCount { get; private set; }
        public int DeviceCodeCount { get; private set; }
        public int ExpiresInSeconds { get; set; } = 900;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var uri = request.RequestUri!;
            var stage = uri.AbsolutePath.EndsWith("/devicecode", StringComparison.Ordinal) ? "device-code"
                : uri.AbsolutePath.EndsWith("/token", StringComparison.Ordinal) ? "token"
                : uri.Host == "user.auth.xboxlive.com" ? "xbox"
                : uri.AbsolutePath.EndsWith("/profile", StringComparison.Ordinal) ? "profile"
                : "exchange";
            if (stage == BlockStage)
            {
                Blocked = true;
                try { await Task.Delay(Timeout.Infinite, cancellationToken); }
                catch (OperationCanceledException) { Canceled = true; throw; }
            }

            var status = HttpStatusCode.OK;
            string json;
            if (stage == "device-code")
            {
                DeviceCodeCount++;
                json = JsonSerializer.Serialize(new
                {
                    device_code = "private-device-code-" + DeviceCodeCount,
                    user_code = UserCode, verification_uri = "https://example.invalid/device", expires_in = ExpiresInSeconds, interval = 1
                });
            }
            else if (stage == "token")
            {
                PollCount++;
                if (!Authorized || Outcome is "expired_token" or "authorization_declined")
                {
                    status = HttpStatusCode.BadRequest;
                    json = JsonSerializer.Serialize(new { error = Authorized ? Outcome : "authorization_pending" });
                }
                else json = """{"access_token":"oauth-token","refresh_token":"refresh-token"}""";
            }
            else if (stage == "xbox") json = """{"Token":"xbox-token"}""";
            else if (uri.Host == "xsts.auth.xboxlive.com") json = """{"Token":"xsts-token","DisplayClaims":{"xui":[{"uhs":"user-hash"}]}}""";
            else if (uri.AbsolutePath.EndsWith("/login_with_xbox", StringComparison.Ordinal)) json = """{"access_token":"minecraft-token"}""";
            else if (uri.AbsolutePath.EndsWith("/mcstore", StringComparison.Ordinal))
                json = Outcome == "no-ownership" ? """{"items":[]}""" : """{"items":[{"name":"game_minecraft"}]}""";
            else if (stage == "profile") json = JsonSerializer.Serialize(new { id = MicrosoftProfileId, name = "SignedInPlayer", skins = Array.Empty<object>() });
            else throw new AssertFailedException("Unexpected login request: " + uri);
            return new HttpResponseMessage(status) { Content = new StringContent(json) };
        }
    }
}
