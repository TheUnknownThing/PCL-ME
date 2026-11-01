using System.IO.Compression;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.VisualTree;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.App.Tasks;
using PCL.Core.Minecraft.Launch;
using PCL.Extensions.Avalonia;
using PCL.Extensions.Contracts;
using PCL.Extensions.Runtime;
using PCL.Frontend.Avalonia.Desktop.Extensions;
using PCL.Frontend.Avalonia.Desktop.Panes.Right.Sections;
using PCL.Frontend.Avalonia.Models;
using PCL.Frontend.Avalonia.Workflows;
using static PCL.Frontend.Avalonia.E2E.DesktopFixture;

namespace PCL.Frontend.Avalonia.E2E;

public sealed partial class ExtensionCapabilitiesE2E
{
    [TestMethod]
    public Task ExternalPresetRunsAfterNativeInstallation_AndWorldActionProducesARealBackupArchive() => Run(async () =>
    {
        await using var server = new ExtensionTestRepository();
        await using var app = new DesktopFixture(); var sourceId = CreateInstance(app, "backup-source", "1.20.1");
        var world = Path.Combine(sourceId, "saves", "BackupWorld"); Directory.CreateDirectory(world); File.WriteAllText(Path.Combine(world, "level.dat"), "backed up world");
        await app.Start();
        var release = Release(server, "preset", "preset.jar", "post-install preset");
        var catalog = server.AddJson("/preset-catalog.json", new { projects = new[] { new { id = "preset-mod", title = "Preset mod", kind = ExtensionResourceKind.Mod, releases = new[] { release } } } });
        var example = Path.Combine(AppContext.BaseDirectory, "WorkflowExample");
        var package = Path.Combine(app.Root, "preset-example.pclext");
        using (var zip = ZipFile.Open(package, ZipArchiveMode.Create))
            foreach (var file in Directory.EnumerateFiles(example, "*", SearchOption.AllDirectories)) zip.CreateEntryFromFile(file, Path.GetRelativePath(example, file).Replace('\\', '/'));
        var info = await app.Session.Host.InstallAsync(package);
        info.Settings.Set("catalogUrl", catalog); info.Settings.Set("presetProvider", "example.workflows.catalog"); info.Settings.Set("presetProjects", ""); info.Settings.Set("recordProjects", true);
        await app.Session.Host.SetEnabledAsync(info.Manifest.Id, true);
        var consumer = await Activate(app);
        var resources = consumer.GetService<IExtensionResources>();
        var sourcePlan = await resources.PrepareInstallAsync(new() { InstanceId = sourceId, Resources = [new() { ProviderId = "example.workflows.catalog", ProjectId = "preset-mod" }] });
        await resources.ApplyInstallAsync(sourcePlan.Id);
        await Wait(() => info.Settings.Get("recentProjects", Array.Empty<ExtensionResourceReference>()).Any(resource => resource.ProjectId == "preset-mod"), "The external recorder did not retain installed project IDs.");
        var clientBytes = Encoding.UTF8.GetBytes("preset test client");
        var client = server.Add("/preset-client.jar", clientBytes);
        var manifest = server.AddJson("/preset-version.json", new { id = "1.20.1", clientVersion = "1.20.1", type = "release", mainClass = "net.minecraft.client.main.Main", libraries = Array.Empty<object>(),
            downloads = new { client = new { url = client, sha1 = Convert.ToHexString(System.Security.Cryptography.SHA1.HashData(clientBytes)).ToLowerInvariant(), size = clientBytes.Length } } });
        SeedCatalog(app.Launcher, manifest);
        await app.Session.NavigateAsync("Download/DownloadInstall"); await Settle();
        await Wait(() => app.Window.GetVisualDescendants().OfType<PclListItem>().Any(item => item.Title == "1.20.1" && item.IsEffectivelyVisible), "The native installation catalog did not render.");
        await Click(app.Window, () => app.Window.GetVisualDescendants().OfType<PclListItem>().SingleOrDefault(item => item.Title == "1.20.1" && item.IsEffectivelyVisible));
        await Wait(() => app.Window.GetVisualDescendants().OfType<CheckBox>().Any(box => (string?)box.Content == "Install this preset after the next matching instance is created" && box.IsEffectivelyVisible), "The install preset contribution did not appear.");
        var rememberedBox = app.Window.Find<CheckBox>("ExampleRememberedMods")!;
        await Click(app.Window, rememberedBox);
        Assert.IsTrue(app.Window.Find<CheckBox>("ExampleRememberedMods")!.IsChecked, "The preset checkbox must remain clickable after scrolling the installation page.");
        try { await Click(app.Window, app.Window.GetVisualDescendants().OfType<CheckBox>().Single(box => (string?)box.Content == "Install this preset after the next matching instance is created")); }
        catch { app.Screenshot("preset-checkbox-failure"); throw; }
        Assert.IsTrue(app.Window.GetVisualDescendants().OfType<CheckBox>().Single(box => (string?)box.Content == "Install this preset after the next matching instance is created").IsChecked);
        var targetName = app.Launcher.DownloadInstallName;
        try { await Click(app.Window, app.Window.GetVisualDescendants().OfType<PclButton>().Single(button => button.IsEffectivelyVisible && button.Text == app.Launcher.DownloadInstallStartText)); }
        catch { app.Screenshot("preset-start-failure"); throw; }
        var target = Path.Combine(app.Root, "minecraft", "versions", targetName);
        try { await Wait(() => TaskCenter.Tasks.Any(task => task.Title == "Common mods" && task.State == TaskState.Success), "The post-install preset did not complete."); }
        catch { Assert.Fail(string.Join(" | ", TaskCenter.Tasks.Select(task => $"{task.Title}: {task.State}: {task.StateMessage}").Concat(app.Session.Host.Diagnostics.Select(item => item.Message)))); }
        var gameDirectory = FrontendInstanceCompositionService.Compose(app.Paths, targetName, FrontendInstanceCompositionService.LoadMode.Lightweight).Selection.IndieDirectory;
        Assert.AreEqual("post-install preset", File.ReadAllText(Path.Combine(gameDirectory, "mods", "preset.jar")));
        CollectionAssert.AreEqual(clientBytes, File.ReadAllBytes(Path.Combine(target, targetName + ".jar")));
        Assert.IsTrue(TaskCenter.Tasks.Any(task => task.Title.StartsWith(targetName, StringComparison.Ordinal) && task.State == TaskState.Success));
        await app.Session.NavigateAsync("instance", new Dictionary<string, string> { ["id"] = sourceId });
        await app.Session.NavigateAsync("InstanceSetup/VersionWorld"); await Settle();
        Grid? row = null; Point? point = null;
        await Wait(() =>
        {
            row = app.Window.GetVisualDescendants().OfType<Grid>().SingleOrDefault(grid => ExtensionContextActions.GetResourcePath(grid) == world);
            point = row?.TranslatePoint(new Point(row.Bounds.Width / 2, row.Bounds.Height / 2), app.Window);
            return point is { } position && app.Window.InputHitTest(position) is Visual hit && (ReferenceEquals(hit, row) || hit.GetVisualAncestors().Contains(row!));
        }, "The world backup target did not render.");
        app.Window.MouseMove(point!.Value); app.Window.MouseDown(point.Value, MouseButton.Right, RawInputModifiers.None); app.Window.MouseUp(point.Value, MouseButton.Right, RawInputModifiers.None);
        try { await Wait(() => app.Session.OpenContributionMenus.Count > 0, "The external world action was not available."); }
        catch { app.Screenshot("preset-world-failure"); Assert.Fail($"World menu: {info.Status}; {app.Session.CanInteract}; target {sourceId}; selected {app.Launcher.ExtensionSelectedInstanceId}; location {ExtensionContextActions.GetLocation(row!)}; {string.Join(" | ", app.Session.Host.Diagnostics.Select(item => item.Message))}"); }
        var menu = app.Session.OpenContributionMenus.Single(); await Settle();
        var backup = menu.GetVisualDescendants().OfType<MenuItem>().Single(item => (string?)item.Header == "Back up world");
        await Click(TopLevel.GetTopLevel(backup)!, backup);
        await Wait(() => TaskCenter.Tasks.Any(task => task.Title == "Back up world" && task.State == TaskState.Success), "The world backup did not complete.");
        var directory = Path.Combine(app.Paths.DataDirectory, "Extensions", "Data", "example.workflows", "Backups");
        using var archived = ZipFile.OpenRead(Directory.GetFiles(directory, "*.zip").Single());
        using var reader = new StreamReader(archived.GetEntry("level.dat")!.Open());
        Assert.AreEqual("backed up world", await reader.ReadToEndAsync());
        Assert.AreEqual("backed up world", File.ReadAllText(Path.Combine(world, "level.dat")));
    });

    [TestMethod]
    public Task ExternalWorkflowExample_ConfiguresCatalog_SearchesSkins_ReviewsFiles_AndCleansUpOnDisable() => Run(async () =>
    {
        await using var server = new ExtensionTestRepository();
        await using var app = new DesktopFixture(); var id = CreateInstance(app, "example", "1.20.1"); await app.Start();
        var release = Release(server, "example-skin", "example.png", "reference extension skin") with { Kind = ExtensionResourceKind.Skin };
        var catalog = server.AddJson("/catalog.json", new { projects = new[] { new { id = "test-skin", title = "Test skin", description = "Skin from the local test repository", kind = ExtensionResourceKind.Skin, releases = new[] { release } } } });
        var source = Path.Combine(AppContext.BaseDirectory, "WorkflowExample");
        var package = Path.Combine(app.Root, "workflows.pclext");
        using (var zip = ZipFile.Open(package, ZipArchiveMode.Create))
            foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
                zip.CreateEntryFromFile(file, Path.GetRelativePath(source, file).Replace('\\', '/'));
        var info = await app.Session.Host.InstallAsync(package);
        await app.OpenExtensions(); await app.Expand(info.Manifest.Id);
        var field = app.Card(info.Manifest.Id).Find<TextBox>("Setting_catalogUrl")!;
        await Type(app.Window, field, catalog);
        Assert.AreEqual(catalog, info.Settings.Get("catalogUrl", ""));
        await app.Toggle(info.Manifest.Id);
        Assert.AreEqual(ExtensionStatus.Active, info.Status);
        await app.Session.NavigateAsync("instance", new Dictionary<string, string> { ["id"] = id });
        await app.Search("Skins");
        app.Window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None); app.Window.KeyReleaseQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        await Wait(() => app.Window.Find<PclSearchBox>("ExampleSkinQuery") is { IsEffectivelyVisible: true }, "Global search did not open the extension's contextual page.");
        await Type(app.Window, app.Window.Find<PclSearchBox>("ExampleSkinQuery")!.Find<TextBox>("SearchTextBox")!, "Test");
        await Click(app.Window, app.Window.Find<PclButton>("ExampleSkinSearch")!);
        await Wait(() => app.Window.Find<PclButton>("ExampleSkin_test-skin") is not null, "The external provider did not search its repository.");
        await Click(app.Window, app.Window.Find<PclButton>("ExampleSkin_test-skin")!);
        await Wait(() => app.Window.Find<PclButton>("ExampleApplyInstall") is not null, "The external example did not present its installation plan.");
        Assert.IsFalse(File.Exists(Path.Combine(id, "skins", "example.png")));
        await Click(app.Window, app.Window.Find<PclButton>("ExampleApplyInstall")!);
        await Wait(() => app.Window.Find<TextBlock>("ExampleFeedback")?.Text == "Completed.", "The external sample installation failed.");
        Assert.AreEqual("reference extension skin", File.ReadAllText(Path.Combine(id, "skins", "example.png")));
        Assert.IsTrue(server.Requests.Contains("/catalog.json"));
        await app.Session.Host.SetEnabledAsync(info.Manifest.Id, false); await Settle();
        Assert.IsFalse(app.Launcher.HasExtensionPage);
        Assert.IsFalse(app.Session.GetLocations().Any(location => location.Route.Contains("example.workflows", StringComparison.Ordinal)));
    });

    [TestMethod]
    public Task MetadataCommitFailureRollsBackAnAlreadyReplacedFileAndPreservesOriginalMetadataPath() => Run(async () =>
    {
        await using var server = new ExtensionTestRepository();
        await using var app = new DesktopFixture(); var id = CreateInstance(app, "rollback", "1.20.1");
        Directory.CreateDirectory(Path.Combine(id, "mods")); File.WriteAllText(Path.Combine(id, "mods", "original.jar"), "original bytes");
        var indexPath = Path.Combine(id, "PCL", "downloaded-resources.v1.json"); Directory.CreateDirectory(indexPath);
        await app.Start();
        var provider = new ExtensionTestProvider();
        provider.Releases["main"] = [Release(server, "replacement", "original.jar", "replacement bytes")];
        var context = await Provider(app, provider); var resources = context.GetService<IExtensionResources>();
        var plan = await resources.PrepareInstallAsync(Request(id, "main"));
        CollectionAssert.AreEqual(new[] { Path.Combine(id, "mods", "original.jar") }, plan.Entries.Single().ReplacedPaths.ToArray());
        var error = await Assert.ThrowsAsync<Exception>(async () => await resources.ApplyInstallAsync(plan.Id));
        Assert.IsTrue(error is IOException or UnauthorizedAccessException);
        Assert.AreEqual("original bytes", File.ReadAllText(Path.Combine(id, "mods", "original.jar")));
        Assert.IsTrue(Directory.Exists(indexPath), "Rollback must preserve the metadata path that existed before installation.");
        Assert.IsFalse(Directory.EnumerateDirectories(id, ".pcl-extension-stage-*").Any());
        Assert.IsTrue(server.Requests.Contains("/replacement"), "This failure must occur during commit, after the download succeeded.");
    });

    [TestMethod]
    public Task LaunchButtonRunsHooks_AndGameEventsKeepTheLaunchedInstanceAfterSelectionChanges() => Run(async () =>
    {
        await using var app = new DesktopFixture();
        var id = CreateInstance(app, "launch-ui", "1.20.1");
        var other = CreateInstance(app, "other-ui", "1.20.1");
        File.WriteAllText(Path.Combine(id, "launch-ui.jar"), "client");
        var probe = Path.Combine(AppContext.BaseDirectory, "Compatibility", "ProcessProbe", "ExtensionProcessProbe" + (OperatingSystem.IsWindows() ? ".exe" : ""));
        if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(probe, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        var local = app.Paths.OpenLocalConfigProvider();
        local.Set("LaunchInstanceSelect", "launch-ui");
        local.Set("LaunchArgumentJavaUser", JsonSerializer.Serialize(new[] { new { Path = probe, IsEnable = true, Installation = new
        { JavaExePath = probe, DisplayName = "Java process probe", Version = "17.0.1", MajorVersion = 17, Is64Bit = true, IsJre = false, Brand = "OpenJDK", Architecture = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString() } } }));
        local.Sync();
        var shared = app.Paths.OpenSharedConfigProvider(); shared.Set("LaunchArgumentJavaSelect", probe); shared.Sync();
        var config = FrontendRuntimePaths.OpenInstanceConfigProvider(id); config.Set("VersionAdvanceAssetsV2", true); config.Sync();
        var profiles = FrontendProfileStorageService.CreateOfflineProfile(MinecraftLaunchProfileDocument.Empty, "ExtensionTest", "0123456789abcdef0123456789abcdef");
        profiles = profiles with { Profiles = [profiles.Profiles[0] with { SkinHeadId = "Alex" }] };
        FrontendProfileStorageService.Save(app.Paths, profiles);
        await app.Start();
        var output = Path.Combine(app.Root, "ui-process.json");
        var release = Path.Combine(app.Root, "release-process");
        var events = new List<ExtensionEvent>();
        var context = await Activate(app, context =>
        {
            context.Events.Subscribe(ExtensionTopics.GameStarted, events.Add);
            context.Events.Subscribe(ExtensionTopics.GameExited, events.Add);
            context.GetService<IExtensionLaunchHooks>().Register(new() { Id = "test.capabilities.ui-launch" }, (_, _) => ValueTask.FromResult(new ExtensionLaunchChanges
            {
                JvmArguments = ["-Dextension.ui-launch=true"], Environment = new Dictionary<string, string>
                { ["PCL_EXTENSION_PROBE_RESULT"] = output, ["PCL_EXTENSION_PROBE_RELEASE"] = release, ["PCL_EXTENSION_VALUE"] = "UI launch" }
            }));
            return ValueTask.CompletedTask;
        });
        try
        {
            await context.Launcher.NavigateAsync("Launch"); await Settle();
            var launch = app.Window.GetVisualDescendants().OfType<Desktop.Controls.PclLaunchButton>().Single(button => button.IsEffectivelyVisible);
            await Click(app.Window, launch);
            await Settle();
            for (var attempt = 0; attempt < 4 && app.Launcher.IsPromptOverlayVisible; attempt++)
            {
                var proceed = app.Window.GetVisualDescendants().OfType<PclButton>().Single(button => button.IsEffectivelyVisible && button.Text == "Continue");
                await Click(app.Window, proceed); await Settle();
            }
            app.Screenshot("extension-launch-ui");
            await Wait(() => File.Exists(output), "The launch button did not run the hooked process. " + app.Launcher.LaunchButtonTitle);
            await Wait(() => events.Any(item => item.Topic == ExtensionTopics.GameStarted), "The actual game start event was not published.");
            using var result = JsonDocument.Parse(File.ReadAllText(output));
            Assert.IsTrue(result.RootElement.GetProperty("Arguments").EnumerateArray().Any(argument => argument.GetString() == "-Dextension.ui-launch=true"));
            Assert.AreEqual("UI launch", result.RootElement.GetProperty("Value").GetString());
            await context.Launcher.NavigateAsync("instance", new Dictionary<string, string> { ["id"] = other });
            File.WriteAllText(release, "exit");
            await Wait(() => events.Any(item => item.Topic == ExtensionTopics.GameExited), "The actual game exit event was not published.");
            Assert.IsTrue(events.All(item => item.Data.GetProperty("InstanceId").GetString() == id));
            Assert.AreEqual(0, events.Single(item => item.Topic == ExtensionTopics.GameExited).Data.GetProperty("ExitCode").GetInt32());
        }
        finally { File.WriteAllText(release, "exit"); }
    });

    [TestMethod]
    public Task ContextActions_RealInstanceModWorldAndResourceMenus_CaptureTargets_AndCloseOnDisable() => Run(async () =>
    {
        await using var app = new DesktopFixture();
        var first = CreateInstance(app, "actions", "1.20.1");
        var second = CreateInstance(app, "other", "1.20.1");
        var manifestPath = Path.Combine(first, "actions.json");
        var manifest = JsonNode.Parse(File.ReadAllText(manifestPath))!;
        manifest["libraries"] = new JsonArray(new JsonObject { ["name"] = "net.fabricmc:fabric-loader:0.16.1" });
        File.WriteAllText(manifestPath, manifest.ToJsonString());
        Directory.CreateDirectory(Path.Combine(first, "mods")); File.WriteAllText(Path.Combine(first, "mods", "action.jar"), "mod");
        Directory.CreateDirectory(Path.Combine(first, "saves", "ActionWorld")); File.WriteAllText(Path.Combine(first, "saves", "ActionWorld", "level.dat"), "world");
        Directory.CreateDirectory(Path.Combine(first, "resourcepacks")); File.WriteAllText(Path.Combine(first, "resourcepacks", "action.zip"), "resource");
        await app.Start();
        var invocations = new List<(ExtensionContextActionLocation, ExtensionActionContext)>();
        var context = await Activate(app, context =>
        {
            var contributions = context.GetService<IExtensionContributions>();
            foreach (var location in Enum.GetValues<ExtensionContextActionLocation>())
                contributions.RegisterAction(new() { Id = "test.capabilities.action." + location.ToString().ToLowerInvariant(), Title = "Inspect " + location, Location = location },
                    (target, _) => { invocations.Add((location, target)); return ValueTask.CompletedTask; });
            return ValueTask.CompletedTask;
        });
        await Select(first); await Settle();
        await Wait(() => app.Window.GetVisualDescendants().OfType<PclCard>().Any(card => card.IsEffectivelyVisible), "Instance overview did not render.");
        await RightClick(() => app.Window.GetVisualDescendants().OfType<PclCard>().FirstOrDefault(card => card.IsEffectivelyVisible));
        await Invoke("Inspect Instance");
        Assert.AreEqual(first, invocations.Last().Item2.InstanceId);
        await context.Launcher.NavigateAsync("InstanceSetup/VersionMod");
        await Wait(() => app.Window.GetVisualDescendants().OfType<ResourceEntryCardView>().Any(view => view.IsEffectivelyVisible), "Mod row missing.");
        await RightClick(() => app.Window.GetVisualDescendants().OfType<ResourceEntryCardView>().FirstOrDefault(view => view.IsEffectivelyVisible && ExtensionContextActions.GetResourcePath(view) == Path.Combine(first, "mods", "action.jar")));
        await Invoke("Inspect Mod");
        Assert.AreEqual(Path.Combine(first, "mods", "action.jar"), invocations.Last().Item2.ResourcePath);
        await context.Launcher.NavigateAsync("InstanceSetup/VersionWorld");
        await Wait(() => app.Window.GetVisualDescendants().OfType<Grid>().Any(grid => ExtensionContextActions.GetResourcePath(grid) == Path.Combine(first, "saves", "ActionWorld")), "World row missing.");
        await RightClick(() => app.Window.GetVisualDescendants().OfType<Grid>().SingleOrDefault(grid => ExtensionContextActions.GetResourcePath(grid) == Path.Combine(first, "saves", "ActionWorld")));
        await Invoke("Inspect World");
        Assert.AreEqual(Path.Combine(first, "saves", "ActionWorld"), invocations.Last().Item2.ResourcePath);
        await context.Launcher.NavigateAsync("InstanceSetup/VersionResourcePack");
        await Wait(() => app.Window.GetVisualDescendants().OfType<ResourceEntryCardView>().Any(view => view.IsEffectivelyVisible), "Resource row missing.");
        await RightClick(() => app.Window.GetVisualDescendants().OfType<ResourceEntryCardView>().FirstOrDefault(view => view.IsEffectivelyVisible && ExtensionContextActions.GetResourcePath(view) == Path.Combine(first, "resourcepacks", "action.zip")));
        await Invoke("Inspect Resource");
        Assert.AreEqual(Path.Combine(first, "resourcepacks", "action.zip"), invocations.Last().Item2.ResourcePath);
        var captured = app.Session.CreateContributionMenu(ExtensionContextActionLocation.Instance, new() { InstanceId = first });
        await Select(second); captured.Open(app.Window); await Settle();
        var item = captured.GetVisualDescendants().OfType<MenuItem>().Single(entry => (string?)entry.Header == "Inspect Instance");
        await Click(TopLevel.GetTopLevel(item)!, item);
        Assert.AreEqual(first, invocations.Last().Item2.InstanceId, "An action must use its captured target, even after global selection changes.");
        var pending = app.Session.CreateContributionMenu(ExtensionContextActionLocation.Instance, new() { InstanceId = second });
        pending.Open(app.Window); await Settle();
        await app.Session.Host.SetEnabledAsync(context.Manifest.Id, false);
        Assert.IsFalse(pending.IsOpen);
        Assert.AreEqual(0, app.Session.CreateContributionMenu(ExtensionContextActionLocation.Instance, new() { InstanceId = second }).ItemCount);

        Task Select(string id) => context.Launcher.NavigateAsync("instance", new Dictionary<string, string> { ["id"] = id }).AsTask();
        async Task RightClick(Func<Control?> resolve)
        {
            Control? target = null;
            await Wait(() => (target = resolve()) is { IsEffectivelyVisible: true }, "The context target did not appear.");
            target!.BringIntoView(); await Settle();
            Point? point = null;
            await Wait(() =>
            {
                target = resolve();
                if (target is null) return false;
                point = target.TranslatePoint(new Point(target.Bounds.Width / 2, target.Bounds.Height / 2), app.Window);
                return point is { } position && app.Window.InputHitTest(position) is Visual hit && (ReferenceEquals(hit, target) || hit.GetVisualAncestors().Contains(target));
            }, "The context target is covered.");
            app.Window.MouseMove(point!.Value); app.Window.MouseDown(point.Value, MouseButton.Right, RawInputModifiers.None); app.Window.MouseUp(point.Value, MouseButton.Right, RawInputModifiers.None);
            await Wait(() => app.Session.OpenContributionMenus.Count > 0, "Context menu did not open.");
        }
        async Task Invoke(string title)
        {
            var menu = app.Session.OpenContributionMenus.Last();
            var item = menu.GetVisualDescendants().OfType<MenuItem>().Single(entry => (string?)entry.Header == title);
            var count = invocations.Count;
            await Click(TopLevel.GetTopLevel(item)!, item);
            await Wait(() => invocations.Count == count + 1, "Context action did not execute.");
        }
    });

    [TestMethod]
    public Task UnrelatedRegistrationsPreserveEditors_AndFactoryFailuresDoNotRemoveHealthySections() => Run(async () =>
    {
        await using var app = new DesktopFixture(); await app.Start();
        var context = await Activate(app, context =>
        {
            context.GetService<IExtensionContributions>().RegisterSection(new() { Id = "test.capabilities.editor", Title = "Editor", Region = ExtensionRegions.SettingsSections },
                _ => new TextBox { Name = "PersistentExtensionEditor", Text = "initial" });
            return ValueTask.CompletedTask;
        });
        await app.OpenExtensions();
        var editor = app.Window.Find<TextBox>("PersistentExtensionEditor")!;
        await Type(app.Window, editor, "unsaved text");
        var other = await Activate(app, context =>
        {
            var ui = context.GetService<IExtensionContributions>();
            ui.RegisterSection(new() { Id = "test.other.failure", Title = "Failure", Region = ExtensionRegions.SettingsSections }, _ => throw new InvalidOperationException("factory failed"));
            ui.RegisterSection(new() { Id = "test.other.healthy", Title = "Healthy", Region = ExtensionRegions.SettingsSections }, _ => new TextBlock { Name = "HealthyExtensionSection", Text = "healthy" });
            return ValueTask.CompletedTask;
        }, "test.other");
        await Settle();
        Assert.AreSame(editor, app.Window.Find<TextBox>("PersistentExtensionEditor"));
        Assert.AreEqual("unsaved text", editor.Text);
        Assert.IsNotNull(app.Window.Find<TextBlock>("HealthyExtensionSection"));
        Assert.AreEqual(ExtensionStatus.Active, app.Session.Host.Get(other.Manifest.Id).Status);
        Assert.IsTrue(app.Session.Host.Diagnostics.Any(item => item.ExtensionId == other.Manifest.Id && item.Message.Contains("factory failed", StringComparison.Ordinal)));
        await app.Session.Host.SetEnabledAsync(other.Manifest.Id, false); await Settle();
        Assert.AreSame(editor, app.Window.Find<TextBox>("PersistentExtensionEditor"));
        Assert.IsNull(app.Window.Find<TextBlock>("HealthyExtensionSection"));
    });

    [TestMethod]
    public Task ProviderBrowser_KeyboardSource_Search_Review_Install_AndDisableCleanup() => Run(async () =>
    {
        await using var server = new ExtensionTestRepository();
        await using var app = new DesktopFixture();
        var id = CreateInstance(app, "browser", "1.20.1");
        await app.Start();
        var provider = new ExtensionTestProvider();
        provider.Releases["searchable-mod"] = [Release(server, "browser-main", "browser.jar", "installed from browser") with { Dependencies = [Reference("dependency")] }];
        provider.Releases["dependency"] = [Release(server, "browser-dependency", "dependency.jar", "browser dependency")];
        var context = await Provider(app, provider);
        await context.Launcher.NavigateAsync("instance", new Dictionary<string, string> { ["id"] = id });
        await context.Launcher.NavigateAsync("Download/DownloadMod");
        await Wait(() => app.Launcher.DownloadResourceSourceOptions.Any(option => option.FilterValue == "test.capabilities.repository"), "The registered source did not enter the download picker.");
        var source = app.Window.Find<ComboBox>("DownloadResourceSourceComboBox")!;
        await Click(app.Window, source);
        app.Window.KeyPressQwerty(PhysicalKey.End, RawInputModifiers.None); app.Window.KeyReleaseQwerty(PhysicalKey.End, RawInputModifiers.None);
        app.Window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None); app.Window.KeyReleaseQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        await Wait(() => app.Launcher.SelectedDownloadResourceSourceOption?.FilterValue == "test.capabilities.repository", "Keyboard source selection failed.");
        var search = app.Window.GetVisualDescendants().OfType<PclSearchBox>().First(box => box.IsEffectivelyVisible);
        var pendingSearch = new TaskCompletionSource<ExtensionResourcePage>(TaskCreationOptions.RunContinuationsAsynchronously);
        provider.Search = (_, _) => new(pendingSearch.Task);
        await Type(app.Window, search.Find<TextBox>("SearchTextBox")!, "before-edit");
        app.Window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None); app.Window.KeyReleaseQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        await Wait(() => provider.Queries.Any(query => query.Text == "before-edit"), "The controlled provider request did not start.");
        await Type(app.Window, search.Find<TextBox>("SearchTextBox")!, "searchable");
        pendingSearch.SetResult(new() { Projects = [new() { ProviderId = "test.capabilities.repository", ProjectId = "stale", Title = "Stale result" }] });
        await Wait(() => !app.Launcher.ShowDownloadResourceLoadingCard, "The completed stale request did not clear its loading state.");
        Assert.AreEqual("searchable", app.Launcher.DownloadResourceSearchQuery, "A completed request must preserve newer edits awaiting Search.");
        Assert.AreEqual("test.capabilities.repository", app.Launcher.SelectedDownloadResourceSourceOption?.FilterValue);
        Assert.IsFalse(app.Launcher.DownloadResourceEntries.Any(entry => entry.Title == "Stale result"));
        provider.Search = null;
        app.Window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None); app.Window.KeyReleaseQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        app.Screenshot("extension-provider-search");
        await Wait(() => app.Launcher.DownloadResourceEntries.Count == 1 && app.Launcher.DownloadResourceEntries[0].Title == "searchable-mod", "The provider search did not render.");
        await Wait(() => app.Window.GetVisualDescendants().OfType<Button>().Any(button => button.IsEffectivelyVisible && button.DataContext == app.Launcher.DownloadResourceEntries[0]), "The provider result row did not finish entering the view.");
        await Click(app.Window, () => app.Window.GetVisualDescendants().OfType<Button>().SingleOrDefault(button => button.IsEffectivelyVisible && button.DataContext == app.Launcher.DownloadResourceEntries[0]));
        await Wait(() => app.Window.Find<PclButton>("ReviewResource_browser-main") is { IsEffectivelyVisible: true }, "The native provider release view did not open.");
        await Click(app.Window, app.Window.Find<PclButton>("ReviewResource_browser-main")!);
        await Wait(() => app.Window.Find<StackPanel>("ExtensionInstallPreview") is not null, "No dependency plan was presented.");
        Assert.IsTrue(app.Window.Find<StackPanel>("ExtensionInstallPreview")!.GetVisualDescendants().OfType<TextBlock>().Any(text => text.Text!.Contains("dependency.jar", StringComparison.Ordinal)));
        Assert.IsFalse(File.Exists(Path.Combine(id, "mods", "browser.jar")));
        app.Screenshot("extension-install-review");
        await Click(app.Window, app.Window.Find<PclButton>("ApplyExtensionInstall")!);
        await Wait(() => app.Window.Find<TextBlock>("ExtensionResourceFeedback")?.Text == "Installed successfully.", "The reviewed provider installation did not finish.");
        Assert.AreEqual("installed from browser", File.ReadAllText(Path.Combine(id, "mods", "browser.jar")));
        Assert.AreEqual("browser dependency", File.ReadAllText(Path.Combine(id, "mods", "dependency.jar")));
        await app.Session.Host.SetEnabledAsync(context.Manifest.Id, false); await Settle();
        Assert.IsFalse(app.Window.Find<ContentControl>("ExtensionWorkspace")!.IsVisible);
        Assert.IsFalse(app.Launcher.DownloadResourceSourceOptions.Any(option => option.FilterValue == "test.capabilities.repository"));
    });

    [TestMethod]
    public Task Providers_Pagination_Cancellation_FailureIsolation_AndRemovalInvalidatePreparedPlans() => Run(async () =>
    {
        await using var server = new ExtensionTestRepository();
        await using var app = new DesktopFixture();
        var id = CreateInstance(app, "providers", "1.20.1"); await app.Start();
        var provider = new ExtensionTestProvider();
        for (var i = 0; i < 5; i++) provider.Releases["project-" + i] = [Release(server, "release-" + i, i + ".jar", i.ToString())];
        var owner = await Provider(app, provider);
        var consumer = await Activate(app, id: "test.consumer");
        var resources = consumer.GetService<IExtensionResources>();
        var first = await resources.SearchAsync(new() { ProviderId = "test.capabilities.repository", Limit = 2 });
        var second = await resources.SearchAsync(new() { ProviderId = "test.capabilities.repository", Limit = 2, Offset = 2 });
        Assert.AreEqual(2, first.Projects.Count); Assert.IsTrue(first.HasMore);
        Assert.IsFalse(first.Projects.Select(project => project.ProjectId).Intersect(second.Projects.Select(project => project.ProjectId)).Any());
        Assert.IsTrue(second.Projects.All(project => project.ProviderId == "test.capabilities.repository"));
        var gate = new TaskCompletionSource<ExtensionResourcePage>(TaskCreationOptions.RunContinuationsAsynchronously);
        provider.Search = (query, token) => query.Text == "old" ? new(gate.Task) : ValueTask.FromResult(new ExtensionResourcePage());
        using var cancellation = new CancellationTokenSource();
        var old = resources.SearchAsync(new() { ProviderId = "test.capabilities.repository", Text = "old" }, cancellation.Token).AsTask();
        cancellation.Cancel(); await Assert.ThrowsAsync<OperationCanceledException>(() => old);
        gate.SetResult(new());
        provider.Search = null;
        var broken = new ExtensionTestProvider { Search = (_, _) => throw new IOException("source offline"), FetchReleases = (_, _) => throw new IOException("releases offline") };
        await Activate(app, context =>
        {
            context.GetService<IExtensionResourceProviders>().Register(new() { Id = "test.broken.repository", Title = "Broken", Kinds = [ExtensionResourceKind.Skin] }, broken);
            return ValueTask.CompletedTask;
        }, "test.broken");
        var skin = Release(server, "skin", "skin.png", "skin") with { Kind = ExtensionResourceKind.Skin };
        provider.Releases["skin"] = [skin];
        var results = await resources.SearchAsync(new() { Kind = ExtensionResourceKind.Skin });
        Assert.IsTrue(results.Projects.Any(project => project.ProjectId == "skin"));
        Assert.AreEqual(ExtensionStatus.Active, app.Session.Host.Get("test.broken").Status);
        Assert.IsTrue(app.Session.Host.Diagnostics.Any(diagnostic => diagnostic.ExtensionId == "test.broken" && diagnostic.Message.Contains("source offline", StringComparison.Ordinal)));
        var providerError = await Assert.ThrowsAsync<ExtensionOperationException>(async () => await resources.GetReleasesAsync(new() { ProviderId = "test.broken.repository", ProjectId = "offline" }));
        Assert.AreEqual("provider.failed", providerError.Code);
        Assert.AreEqual(ExtensionStatus.Active, app.Session.Host.Get(consumer.Manifest.Id).Status);
        var plan = await resources.PrepareInstallAsync(Request(id, "project-0"));
        await app.Session.Host.SetEnabledAsync(owner.Manifest.Id, false);
        var error = await Assert.ThrowsAsync<ExtensionOperationException>(async () => await resources.ApplyInstallAsync(plan.Id));
        Assert.AreEqual("provider.unavailable", error.Code);
        Assert.IsFalse(File.Exists(Path.Combine(id, "mods", "0.jar")));
    });

    [TestMethod]
    public Task World_DataPack_AndSkinInstallsUseCorrectTargets_AndRejectUnsafeArchives() => Run(async () =>
    {
        await using var server = new ExtensionTestRepository();
        await using var app = new DesktopFixture(); var id = CreateInstance(app, "content", "1.20.1"); await app.Start();
        byte[] Archive(bool unsafePath)
        {
            using var stream = new MemoryStream();
            using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, true))
            {
                using (var writer = new StreamWriter(zip.CreateEntry("World/level.dat").Open())) writer.Write("world data");
                using (var writer = new StreamWriter(zip.CreateEntry(unsafePath ? "World/../../escape.txt" : "World/region/chunk.mca").Open())) writer.Write("chunk data");
            }
            return stream.ToArray();
        }
        var provider = new ExtensionTestProvider();
        provider.Releases["world"] = [new() { Id = "world", Title = "World", Kind = ExtensionResourceKind.World, FileName = "DownloadedWorld.zip", DownloadUrl = server.Add("/world", Archive(false)) }];
        provider.Releases["data"] = [Release(server, "data", "data.zip", "datapack") with { Kind = ExtensionResourceKind.DataPack }];
        provider.Releases["skin"] = [Release(server, "skin", "skin.png", "skin bytes") with { Kind = ExtensionResourceKind.Skin }];
        var context = await Provider(app, provider); var resources = context.GetService<IExtensionResources>();
        var world = await resources.PrepareInstallAsync(Request(id, "world")); await resources.ApplyInstallAsync(world.Id);
        var worldPath = Path.Combine(id, "saves", "DownloadedWorld");
        Assert.AreEqual("world data", File.ReadAllText(Path.Combine(worldPath, "level.dat")));
        Assert.AreEqual("chunk data", File.ReadAllText(Path.Combine(worldPath, "region", "chunk.mca")));
        var error = await Assert.ThrowsAsync<ExtensionOperationException>(async () => await resources.PrepareInstallAsync(Request(id, "data")));
        Assert.AreEqual("world.required", error.Code);
        var data = await resources.PrepareInstallAsync(Request(id, "data") with { WorldPath = worldPath }); await resources.ApplyInstallAsync(data.Id);
        Assert.AreEqual("datapack", File.ReadAllText(Path.Combine(worldPath, "datapacks", "data.zip")));
        var secondWorld = Path.Combine(id, "saves", "OtherWorld"); Directory.CreateDirectory(secondWorld); File.WriteAllText(Path.Combine(secondWorld, "level.dat"), "other world");
        data = await resources.PrepareInstallAsync(Request(id, "data") with { WorldPath = secondWorld }); await resources.ApplyInstallAsync(data.Id);
        provider.Releases["data"] = [Release(server, "data-upgrade", "data-2.zip", "upgraded datapack") with { Kind = ExtensionResourceKind.DataPack }];
        var upgrade = await resources.PrepareInstallAsync(Request(id, "data") with { WorldPath = secondWorld });
        CollectionAssert.AreEqual(new[] { Path.Combine(secondWorld, "datapacks", "data.zip") }, upgrade.Entries.Single().ReplacedPaths.ToArray());
        await resources.ApplyInstallAsync(upgrade.Id);
        Assert.IsFalse(File.Exists(Path.Combine(secondWorld, "datapacks", "data.zip")));
        Assert.AreEqual("upgraded datapack", File.ReadAllText(Path.Combine(secondWorld, "datapacks", "data-2.zip")));
        Assert.AreEqual("datapack", File.ReadAllText(Path.Combine(worldPath, "datapacks", "data.zip")), "Upgrading a datapack must preserve installations in other worlds.");
        Assert.AreEqual(2, (await context.GetService<IExtensionInstances>().GetAsync(id)).Resources.Count(resource => resource.Kind == ExtensionResourceKind.DataPack && resource.ProjectId == "data"));
        var skin = await resources.PrepareInstallAsync(Request(id, "skin")); await resources.ApplyInstallAsync(skin.Id);
        Assert.AreEqual("skin bytes", File.ReadAllText(Path.Combine(id, "skins", "skin.png")));
        provider.Releases["world"] = [provider.Releases["world"][0] with { DownloadUrl = server.Add("/unsafe-world", Archive(true)) }];
        var unsafePlan = await resources.PrepareInstallAsync(Request(id, "world"));
        await Assert.ThrowsAsync<InvalidDataException>(async () => await resources.ApplyInstallAsync(unsafePlan.Id));
        Assert.AreEqual("world data", File.ReadAllText(Path.Combine(worldPath, "level.dat")));
        Assert.IsFalse(File.Exists(Path.Combine(id, "escape.txt")));
        Assert.IsFalse(Directory.EnumerateDirectories(id, ".pcl-extension-stage-*").Any());
    });

    [TestMethod]
    public Task LifecycleEventsTrackExternalChanges_AndSubscriptionsStopOnDisable() => Run(async () =>
    {
        await using var app = new DesktopFixture(); var initial = CreateInstance(app, "initial", "1.20.1"); await app.Start();
        var events = new List<ExtensionEvent>();
        var context = await Activate(app, context =>
        {
            foreach (var topic in new[] { ExtensionTopics.InstanceCreated, ExtensionTopics.InstanceUpdated, ExtensionTopics.InstanceRemoved, ExtensionTopics.ResourcesChanged })
                context.Events.Subscribe(topic, events.Add);
            return ValueTask.CompletedTask;
        });
        var created = CreateInstance(app, "external", "1.21.1");
        await Wait(() => events.Any(item => item.Topic == ExtensionTopics.InstanceCreated && item.Data.GetProperty("InstanceId").GetString() == created), "Creating an instance outside the extension API did not publish an event.");
        Directory.CreateDirectory(Path.Combine(initial, "mods")); File.WriteAllText(Path.Combine(initial, "mods", "external.jar"), "external mod");
        await Wait(() => events.Any(item => item.Topic == ExtensionTopics.ResourcesChanged && item.Data.GetProperty("InstanceId").GetString() == initial), "External resource changes did not publish an event.");
        Directory.Delete(created, true);
        await Wait(() => events.Any(item => item.Topic == ExtensionTopics.InstanceRemoved && item.Data.GetProperty("InstanceId").GetString() == created), "Removing an instance did not publish an event.");
        await app.Session.Host.SetEnabledAsync(context.Manifest.Id, false);
        var count = events.Count;
        File.WriteAllText(Path.Combine(initial, "mods", "external.jar"), "changed again");
        await Task.Delay(400); await Settle(); Assert.AreEqual(count, events.Count);
    });

    [TestMethod]
    public Task LaunchHooks_Order_QuotedArguments_Environment_AndExportedScriptAffectARealChildProcess() => Run(async () =>
    {
        await using var app = new DesktopFixture(); var id = CreateInstance(app, "launch", "1.20.1"); await app.Start();
        var output = Path.Combine(app.Root, "process-result.json");
        var order = new List<string>();
        var context = await Activate(app, context =>
        {
            var hooks = context.GetService<IExtensionLaunchHooks>();
            hooks.Register(new() { Id = "test.capabilities.z", Order = 10 }, (_, _) =>
            { order.Add("z"); return ValueTask.FromResult(new ExtensionLaunchChanges { JvmArguments = ["-Dmessage=hello \"world\"\\ $HOME $(literal) 'quote'"], Environment = new Dictionary<string, string> { ["PCL_EXTENSION_VALUE"] = "from hook", ["PCL_EXTENSION_PROBE_RESULT"] = output } }); });
            hooks.Register(new() { Id = "test.capabilities.a", Order = 10 }, (target, _) =>
            { order.Add("a"); Assert.AreEqual(id, target.InstanceId); return ValueTask.FromResult(new ExtensionLaunchChanges { JvmArguments = ["-Dfirst=1"] }); });
            return ValueTask.CompletedTask;
        });
        await context.Launcher.NavigateAsync("instance", new Dictionary<string, string> { ["id"] = id });
        var composition = ProcessComposition(app, id);
        var prepared = await app.Session.PrepareExtensionLaunchAsync(composition, id, CancellationToken.None);
        CollectionAssert.AreEqual(new[] { "a", "z" }, order);
        var started = await Task.Run(() => app.Actions.StartLaunchSession(prepared, id, ensureRequiredArtifacts: false));
        await started.Process.WaitForExitAsync();
        Assert.AreEqual(0, started.Process.ExitCode);
        using var document = JsonDocument.Parse(File.ReadAllText(output));
        CollectionAssert.AreEqual(new[] { "-Dfirst=1", "-Dmessage=hello \"world\"\\ $HOME $(literal) 'quote'", "base argument" }, document.RootElement.GetProperty("Arguments").EnumerateArray().Select(item => item.GetString()!).ToArray());
        Assert.AreEqual("from hook", document.RootElement.GetProperty("Value").GetString());
        var script = File.ReadAllText(started.LaunchScriptPath);
        Assert.IsTrue(script.Contains("PCL_EXTENSION_VALUE", StringComparison.Ordinal) && script.Contains("from hook", StringComparison.Ordinal));
        if (!OperatingSystem.IsWindows())
        {
            File.Delete(output);
            using var exported = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("/bin/sh")
            { ArgumentList = { started.LaunchScriptPath }, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true })!;
            await exported.WaitForExitAsync();
            Assert.AreEqual(0, exported.ExitCode, await exported.StandardError.ReadToEndAsync());
            using var exportedResult = JsonDocument.Parse(File.ReadAllText(output));
            CollectionAssert.AreEqual(document.RootElement.GetProperty("Arguments").EnumerateArray().Select(item => item.GetString()!).ToArray(),
                exportedResult.RootElement.GetProperty("Arguments").EnumerateArray().Select(item => item.GetString()!).ToArray());
        }
        await app.Session.Host.SetEnabledAsync(context.Manifest.Id, false);
        var unchanged = await app.Session.PrepareExtensionLaunchAsync(composition, id, CancellationToken.None);
        Assert.AreSame(composition, unchanged);
    });

    [TestMethod]
    public Task LaunchHooks_Timeout_FailurePolicies_Conflicts_AndCallerCancellationDoNotLeakChanges() => Run(async () =>
    {
        await using var app = new DesktopFixture(); var id = CreateInstance(app, "hook-errors", "1.20.1"); await app.Start();
        IDisposable? lease = null; IExtensionLaunchHooks hooks = null!;
        var context = await Activate(app, context => { hooks = context.GetService<IExtensionLaunchHooks>(); return ValueTask.CompletedTask; });
        var composition = ProcessComposition(app, id);
        var canceled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        lease = hooks.Register(new() { Id = "test.capabilities.timeout", Timeout = TimeSpan.FromMilliseconds(50), FailurePolicy = ExtensionHookFailurePolicy.ContinueLaunch }, async (_, token) =>
        { try { await Task.Delay(Timeout.Infinite, token); } finally { canceled.TrySetResult(); } return new() { JvmArguments = ["-Dlate=1"] }; });
        var continued = await app.Session.PrepareExtensionLaunchAsync(composition, id, CancellationToken.None);
        await canceled.Task.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.AreEqual(composition.SessionStartPlan.ProcessShellPlan.Arguments, continued.SessionStartPlan.ProcessShellPlan.Arguments);
        lease.Dispose();
        lease = hooks.Register(new() { Id = "test.capabilities.failure" }, (_, _) => throw new IOException("sync failed"));
        var error = await Assert.ThrowsAsync<ExtensionOperationException>(async () => await app.Session.PrepareExtensionLaunchAsync(composition, id, CancellationToken.None));
        Assert.AreEqual("hook.failed", error.Code); lease.Dispose();
        using var first = hooks.Register(new() { Id = "test.capabilities.first" }, (_, _) => ValueTask.FromResult(new ExtensionLaunchChanges { Environment = new Dictionary<string, string> { ["PCL_EXTENSION_VALUE"] = "first" } }));
        lease = hooks.Register(new() { Id = "test.capabilities.second", Order = 1 }, (_, _) => ValueTask.FromResult(new ExtensionLaunchChanges { JvmArguments = ["-Dpartial=1"], Environment = new Dictionary<string, string> { ["PCL_EXTENSION_VALUE"] = "second" } }));
        error = await Assert.ThrowsAsync<ExtensionOperationException>(async () => await app.Session.PrepareExtensionLaunchAsync(composition, id, CancellationToken.None));
        Assert.AreEqual("hook.failed", error.Code); lease.Dispose();
        lease = hooks.Register(new() { Id = "test.capabilities.cancel", Order = 1 }, async (_, token) => { await Task.Delay(Timeout.Infinite, token); return new(); });
        using var caller = new CancellationTokenSource();
        var pending = app.Session.PrepareExtensionLaunchAsync(composition, id, caller.Token).AsTask();
        caller.Cancel(); await Assert.ThrowsAsync<OperationCanceledException>(() => pending); lease.Dispose();
        Assert.AreEqual(ExtensionStatus.Active, app.Session.Host.Get(context.Manifest.Id).Status);
        Assert.AreEqual("\"base argument\"", composition.SessionStartPlan.ProcessShellPlan.Arguments);
    });

    private static FrontendLaunchComposition ProcessComposition(DesktopFixture app, string id)
    {
        var composition = (FrontendLaunchComposition)typeof(ViewModels.LauncherViewModel).GetField("_launchComposition", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(app.Launcher)!;
        var executable = Path.Combine(AppContext.BaseDirectory, "Compatibility", "ProcessProbe", "ExtensionProcessProbe" + (OperatingSystem.IsWindows() ? ".exe" : ""));
        if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(executable, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        return composition with { RequiredArtifacts = [], NativeSyncRequest = null, NativePathAliasDirectory = null,
            SessionStartPlan = composition.SessionStartPlan with { ProcessShellPlan = composition.SessionStartPlan.ProcessShellPlan with
            { FileName = executable, Arguments = "\"base argument\"", WorkingDirectory = id, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true } } };
    }
}
