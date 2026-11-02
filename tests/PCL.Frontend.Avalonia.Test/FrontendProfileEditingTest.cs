using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Text.Json.Nodes;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.App.I18n;
using PCL.Core.Minecraft.Launch;
using PCL.Frontend.Avalonia.Cli;
using PCL.Frontend.Avalonia.Desktop.Controls;
using PCL.Frontend.Avalonia.Models;
using PCL.Frontend.Avalonia.ViewModels;
using PCL.Frontend.Avalonia.Workflows;
using static PCL.Frontend.Avalonia.Test.FrontendDeferredContentTest;

namespace PCL.Frontend.Avalonia.Test;

[TestClass]
[DoNotParallelize]
public sealed class FrontendProfileEditingTest
{
    private const string ProfileId = "0123456789abcdef0123456789abcdef";

    [TestMethod]
    public async Task OfflineSkinPicker_PreservesTheSavedSelectionAndSavesTheUsersChoice()
    {
        await FrontendMotionTest.RunAsync(async () =>
        {
            var document = CreateDocument();
            document = document with { Profiles = [document.Profiles[0], document.Profiles[1] with { SkinHeadId = "Alex" }] };
            using var fixture = new ProfileFixture(document);
            var view = new PclLaunchLeftPanel { DataContext = fixture.Launcher };
            var window = new Window { Width = 320, Height = 800, Content = view };
            try
            {
                window.Show();
                fixture.Launcher.EditLaunchProfileCommand.Execute(null);
                Tick();
                var picker = view.GetVisualDescendants().OfType<ComboBox>()
                    .Single(combo => combo.ItemsSource is IReadOnlyList<string> options && options.Contains("Alex"));
                Assert.AreEqual(2, picker.SelectedIndex);
                ((I18nService)GetField(fixture.Launcher, "_i18n")!).SetLocale("zh-Hans");
                Tick();
                Assert.AreEqual(2, picker.SelectedIndex, "Changing locale must preserve the selected skin.");
                picker.SelectedIndex = 1;
                Tick();
                await InvokeAsync(fixture.Launcher, "SubmitOfflineLaunchProfileAsync");
                Assert.AreEqual("Steve", fixture.Load().Profiles[1].SkinHeadId);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [TestMethod]
    public async Task OfflineEditor_CancelsOrUpdatesTheExistingEntryAndPreservesUuid()
    {
        await FrontendMotionTest.RunAsync(async () =>
        {
            var document = CreateDocument();
            using var fixture = new ProfileFixture(document);
            var launcher = fixture.Launcher;
            launcher.EditLaunchProfileCommand.Execute(null);
            Assert.IsTrue(launcher.ShowLaunchOfflineEditor);
            Assert.AreEqual(ProfileId, launcher.LaunchOfflineCustomUuid);
            launcher.LaunchOfflineUserName = "Canceled";
            launcher.SelectedLaunchOfflineSkinIndex = 2;
            launcher.BackLaunchProfileCommand.Execute(null);
            Assert.AreEqual(document.Profiles[1], fixture.Load().Profiles[1]);

            launcher.EditLaunchProfileCommand.Execute(null);
            launcher.LaunchOfflineUserName = "Renamed";
            launcher.SelectedLaunchOfflineSkinIndex = 2;
            await InvokeAsync(launcher, "SubmitOfflineLaunchProfileAsync");

            var saved = fixture.Load();
            Assert.AreEqual(2, saved.Profiles.Count);
            Assert.AreEqual(1, saved.LastUsedProfile);
            Assert.AreEqual(document.Profiles[0], saved.Profiles[0]);
            Assert.AreEqual("Renamed", saved.Profiles[1].Username);
            Assert.AreEqual(ProfileId, saved.Profiles[1].Uuid);
            Assert.AreEqual("Keep this description", saved.Profiles[1].Desc);
            Assert.AreEqual("Alex", saved.Profiles[1].SkinHeadId);
            Assert.IsTrue(launcher.ShowLaunchProfileSummaryCard);
            var composition = (FrontendLaunchComposition)GetField(launcher, "_launchComposition")!;
            var skin = typeof(LauncherViewModel).GetMethod("ResolveLaunchAvatarDefaultSkinName", BindingFlags.Static | BindingFlags.NonPublic)!
                .Invoke(null, [composition.SelectedProfile]);
            Assert.AreEqual("Alex", skin, "The launcher must render the explicitly selected skin.");

            launcher.EditLaunchProfileCommand.Execute(null);
            Assert.AreEqual(2, launcher.SelectedLaunchOfflineSkinIndex);
            launcher.SelectedLaunchOfflineSkinIndex = 0;
            await InvokeAsync(launcher, "SubmitOfflineLaunchProfileAsync");
            Assert.IsNull(fixture.Load().Profiles[1].SkinHeadId, "Automatic selection must survive saving and reopening.");
        });
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task OfflineEditor_RejectsInvalidUuidOrAChangedSavedEntry(bool changeSavedEntry)
    {
        await FrontendMotionTest.RunAsync(async () =>
        {
            using var fixture = new ProfileFixture(CreateDocument());
            fixture.Launcher.EditLaunchProfileCommand.Execute(null);
            var expected = fixture.Load();
            if (changeSavedEntry)
            {
                expected = expected with { Profiles = [expected.Profiles[0], expected.Profiles[1] with { Username = "ChangedElsewhere" }] };
                FrontendProfileStorageService.Save(fixture.Paths, expected);
            }
            else
            {
                fixture.Launcher.LaunchOfflineCustomUuid = "invalid";
            }

            await InvokeAsync(fixture.Launcher, "SubmitOfflineLaunchProfileAsync");
            Assert.IsTrue(fixture.Launcher.HasLaunchOfflineStatus);
            CollectionAssert.AreEqual(expected.Profiles.ToArray(), fixture.Load().Profiles.ToArray());
        });
    }

    [TestMethod]
    [DataRow("success")]
    [DataRow("unauthorized")]
    [DataRow("wrong-role")]
    [DataRow("target-changed")]
    [DataRow("unrelated-change")]
    public async Task AuthlibEditor_ReauthenticatesInPlaceAndKeepsTheSelectedRole(string outcome)
    {
        await FrontendMotionTest.RunAsync(async () =>
        {
            using var server = new AuthlibServer(outcome);
            var document = CreateDocument();
            document = document with
            {
                Profiles = [document.Profiles[0], document.Profiles[1] with
                {
                    Kind = MinecraftLaunchStoredProfileKind.Authlib,
                    Server = server.Url,
                    LoginName = "saved@example.invalid",
                    Password = "old-password",
                    AccessToken = "expired-token",
                    ClientToken = "old-client-token"
                }]
            };
            using var fixture = new ProfileFixture(document);
            document = fixture.Load();
            if (outcome is "target-changed" or "unrelated-change")
            {
                server.BeforeRefresh = () =>
                {
                    var profiles = fixture.Load().Profiles.ToArray();
                    var index = outcome == "target-changed" ? 1 : 0;
                    profiles[index] = profiles[index] with { Desc = "Changed while signing in" };
                    document = document with { Profiles = profiles };
                    FrontendProfileStorageService.Save(fixture.Paths, document);
                };
            }

            fixture.Launcher.EditLaunchProfileCommand.Execute(null);
            Assert.IsTrue(fixture.Launcher.ShowLaunchAuthlibEditor);
            Assert.AreEqual(server.Url, fixture.Launcher.LaunchAuthlibServer);
            Assert.AreEqual("saved@example.invalid", fixture.Launcher.LaunchAuthlibLoginName);
            Assert.AreEqual(string.Empty, fixture.Launcher.LaunchAuthlibPassword);
            fixture.Launcher.LaunchAuthlibPassword = "new-password";
            await InvokeAsync(fixture.Launcher, "SubmitAuthlibLaunchProfileAsync");

            var saved = fixture.Load();
            Assert.AreEqual(2, saved.Profiles.Count);
            Assert.AreEqual(1, saved.LastUsedProfile);
            Assert.AreEqual(document.Profiles[0], saved.Profiles[0]);
            if (outcome is "success" or "unrelated-change")
            {
                Assert.AreEqual(ProfileId, saved.Profiles[1].Uuid);
                Assert.AreEqual("UpdatedRole", saved.Profiles[1].Username);
                Assert.AreEqual("new-token", saved.Profiles[1].AccessToken);
                Assert.AreEqual("new-password", saved.Profiles[1].Password);
                Assert.AreEqual("Keep this description", saved.Profiles[1].Desc);
                Assert.AreEqual(ProfileId, server.RefreshedProfileId);
                Assert.AreEqual(string.Empty, fixture.Launcher.LaunchAuthlibPassword);
                Assert.IsFalse(File.ReadAllText(FrontendProfileStorageService.GetProfilesPath(fixture.Paths)).Contains("new-password"));
            }
            else
            {
                Assert.IsTrue(fixture.Launcher.HasLaunchAuthlibStatus);
                Assert.AreEqual(document.Profiles[1], saved.Profiles[1]);
            }
        });
    }

    [TestMethod]
    public async Task AuthlibEditor_AddingAfterCancelCreatesAnEntryWithoutReplacingThePreviousOne()
    {
        await FrontendMotionTest.RunAsync(async () =>
        {
            using var server = new AuthlibServer("success");
            using var fixture = new ProfileFixture(CreateDocument());
            var original = fixture.Load();
            fixture.Launcher.EditLaunchProfileCommand.Execute(null);
            fixture.Launcher.BackLaunchProfileCommand.Execute(null);
            fixture.Launcher.LoginAuthlibLaunchProfileCommand.Execute(null);
            fixture.Launcher.LaunchAuthlibServer = server.Url;
            fixture.Launcher.LaunchAuthlibLoginName = "new@example.invalid";
            fixture.Launcher.LaunchAuthlibPassword = "new-password";
            await InvokeAsync(fixture.Launcher, "SubmitAuthlibLaunchProfileAsync");
            var saved = fixture.Load();
            Assert.AreEqual(3, saved.Profiles.Count);
            Assert.AreEqual(2, saved.LastUsedProfile);
            CollectionAssert.AreEqual(original.Profiles.ToArray(), saved.Profiles.Take(2).ToArray());
            Assert.AreEqual("new-token", saved.Profiles[2].AccessToken);
            Assert.AreEqual(server.RefreshedProfileId, saved.Profiles[2].Uuid);
        });
    }

    private static MinecraftLaunchProfileDocument CreateDocument()
    {
        var document = FrontendProfileStorageService.CreateOfflineProfile(MinecraftLaunchProfileDocument.Empty, "Other", new string('a', 32));
        document = FrontendProfileStorageService.CreateOfflineProfile(document, "Original", ProfileId);
        return document with { Profiles = [document.Profiles[0], document.Profiles[1] with { Desc = "Keep this description" }] };
    }

    internal static object? GetField(object instance, string name) =>
        instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(instance);

    internal static Task InvokeAsync(object instance, string name) =>
        (Task)instance.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(instance, null)!;

    internal sealed class ProfileFixture : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), "pcl-profile-test", Guid.NewGuid().ToString("N"));
        private readonly I18nService _i18n;
        private readonly LauncherActionService _actions;

        public FrontendRuntimePaths Paths { get; }

        public LauncherViewModel Launcher { get; }

        public ProfileFixture(MinecraftLaunchProfileDocument? document = null)
        {
            Paths = new FrontendRuntimePaths(_root, _root + "/temp", _root + "/data", _root + "/shared",
                _root + "/shared/config.json", _root + "/data/config.yml", _root + "/appdata", []);
            if (document is not null) FrontendProfileStorageService.Save(Paths, document);
            _i18n = new I18nService(new I18nSettingsManager(new FrontendConfigProviderAdapter(Paths)));
            _actions = new LauncherActionService(Paths, new FrontendPlatformAdapter(), () => { }, _i18n);
            Launcher = LauncherViewModel.CreateBootstrap(AvaloniaCommandParser.CreateDefaultOptions(), _actions, _i18n);
        }

        public MinecraftLaunchProfileDocument Load() => FrontendProfileStorageService.Load(Paths).Document;

        public void Dispose()
        {
            _actions.FlushPendingSettings();
            _i18n.Dispose();
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }
    }

    private sealed class AuthlibServer : IDisposable
    {
        private readonly HttpListener _listener = new();
        private readonly Task _worker;
        private volatile bool _disposing;

        public string Url { get; }

        public string? RefreshedProfileId { get; private set; }

        public Action? BeforeRefresh { get; set; }

        public AuthlibServer(string outcome)
        {
            using var portReservation = new TcpListener(IPAddress.Loopback, 0);
            portReservation.Start();
            var port = ((IPEndPoint)portReservation.LocalEndpoint).Port;
            portReservation.Stop();
            Url = $"http://127.0.0.1:{port}";
            _listener.Prefixes.Add(Url + "/");
            _listener.Start();
            _worker = Task.Run(async () =>
            {
                try
                {
                    while (_listener.IsListening)
                    {
                        var context = await _listener.GetContextAsync();
                        string body;
                        if (context.Request.Url!.AbsolutePath.EndsWith("/authenticate", StringComparison.Ordinal))
                        {
                            using var reader = new StreamReader(context.Request.InputStream);
                            var request = JsonNode.Parse(await reader.ReadToEndAsync())!;
                            body = new JsonObject
                            {
                                ["accessToken"] = "authenticated-token",
                                ["clientToken"] = request["clientToken"]!.DeepClone(),
                                ["availableProfiles"] = new JsonArray(
                                    new JsonObject { ["id"] = new string('b', 32), ["name"] = "OtherRole" },
                                    new JsonObject { ["id"] = ProfileId, ["name"] = "UpdatedRole" })
                            }.ToJsonString();
                            if (outcome == "unauthorized")
                            {
                                context.Response.StatusCode = 403;
                                body = "{\"errorMessage\":\"Invalid credentials\"}";
                            }
                        }
                        else if (context.Request.Url.AbsolutePath.EndsWith("/refresh", StringComparison.Ordinal))
                        {
                            using var reader = new StreamReader(context.Request.InputStream);
                            var request = JsonNode.Parse(await reader.ReadToEndAsync())!;
                            RefreshedProfileId = request["selectedProfile"]?["id"]?.ToString();
                            BeforeRefresh?.Invoke();
                            body = new JsonObject
                            {
                                ["accessToken"] = "new-token",
                                ["clientToken"] = request["clientToken"]!.DeepClone(),
                                ["selectedProfile"] = new JsonObject
                                {
                                    ["id"] = outcome == "wrong-role" ? new string('b', 32) : RefreshedProfileId,
                                    ["name"] = "UpdatedRole"
                                }
                            }.ToJsonString();
                        }
                        else
                        {
                            body = "{\"meta\":{\"serverName\":\"Test Auth\"}}";
                        }

                        var bytes = Encoding.UTF8.GetBytes(body);
                        context.Response.ContentType = "application/json";
                        context.Response.ContentLength64 = bytes.Length;
                        await context.Response.OutputStream.WriteAsync(bytes);
                        context.Response.Close();
                    }
                }
                catch (HttpListenerException) when (_disposing || !_listener.IsListening) { }
                catch (ObjectDisposedException) { }
            });
        }

        public void Dispose()
        {
            _disposing = true;
            _listener.Close();
            _worker.GetAwaiter().GetResult();
        }
    }
}
