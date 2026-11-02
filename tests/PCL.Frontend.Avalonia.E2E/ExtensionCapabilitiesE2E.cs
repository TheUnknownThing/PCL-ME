using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;
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
using PCL.Extensions.Avalonia;
using PCL.Extensions.Contracts;
using PCL.Extensions.Runtime;
using PCL.Frontend.Avalonia.Desktop.Extensions;
using PCL.Frontend.Avalonia.Desktop.Panes.Right.Sections;
using PCL.Frontend.Avalonia.ViewModels;
using PCL.Frontend.Avalonia.Workflows;
using static PCL.Frontend.Avalonia.E2E.DesktopFixture;

namespace PCL.Frontend.Avalonia.E2E;

[TestClass]
[DoNotParallelize]
public sealed partial class ExtensionCapabilitiesE2E
{
    [TestMethod]
    [DataRow("Launch", "Launch")]
    [DataRow("Launch", "Download")]
    [DataRow("Download", "Download")]
    [DataRow("Setup", "Settings")]
    [DataRow("Setup", "Tools")]
    [DataRow("Tools", "Tools")]
    public Task NativePage_TitleBarNavigation_ClosesAndDisposesWorkspace(string startRoute, string tabTitle) => Run(async () =>
    {
        await using var app = new DesktopFixture();
        await app.Start();
        var view = new TrackedView("Native page", new());
        var context = await Activate(app, context =>
        {
            context.GetService<IExtensionUi>().RegisterView(new()
            {
                Id = "test.capabilities.page", Title = "Native page", Location = ExtensionViewLocation.Page
            }, _ => view);
            return ValueTask.CompletedTask;
        });
        await app.Session.NavigateAsync(startRoute);
        await context.GetService<IExtensionUi>().ShowViewAsync("test.capabilities.page");
        await Settle();
        var workspace = app.Window.Find<ContentControl>("ExtensionWorkspace")!;
        Assert.IsTrue(workspace.IsVisible);
        Assert.IsTrue(view.IsEffectivelyVisible);
        Assert.IsFalse(view.Disposed);

        await Click(app.Window, app.Window.GetVisualDescendants().OfType<PclTabButton>()
            .Single(tab => tab.IsEffectivelyVisible && tab.Text == tabTitle));

        Assert.IsFalse(workspace.IsVisible, "Title-bar navigation must dismiss the extension page even when the current tab is clicked.");
        Assert.IsNull(workspace.Content);
        Assert.IsTrue(view.Disposed, "Leaving an extension page must release its view.");
        Assert.IsTrue(app.Window.GetVisualDescendants().OfType<PclTabButton>()
            .Single(tab => tab.IsEffectivelyVisible && tab.Text == tabTitle).IsSelected);
        Assert.AreEqual(ExtensionStatus.Active, app.Session.Host.Get(context.Manifest.Id).Status);
    });

    [TestMethod]
    public Task ContextualPages_AllRegions_Selection_Locale_AndDisableReleaseMountedViews() => Run(async () =>
    {
        await using var app = new DesktopFixture();
        var first = CreateInstance(app, "first", "1.20.1");
        var second = CreateInstance(app, "second", "1.21.1");
        await app.Start();
        var mounts = new List<TrackedView>();
        var context = await Activate(app, context =>
        {
            var ui = context.GetService<IExtensionContributions>();
            foreach (var area in Enum.GetValues<ExtensionPageArea>())
                ui.RegisterPage(new() { Id = "test.capabilities.page." + area.ToString().ToLowerInvariant(), Title = "Custom " + area, Area = area }, surface => Track("Page " + area, surface));
            foreach (var region in Regions)
                ui.RegisterSection(new() { Id = "test.capabilities." + region, Region = region, Title = region }, surface => Track(region, surface));
            return ValueTask.CompletedTask;
        });
        await context.Launcher.NavigateAsync("instance", new Dictionary<string, string> { ["id"] = first });
        await Wait(() => mounts.Any(view => view.Region == ExtensionRegions.InstanceOverviewCards && view.IsEffectivelyVisible), "Instance card did not mount.");
        var firstCard = mounts.Last(view => view.Region == ExtensionRegions.InstanceOverviewCards);
        Assert.AreEqual(first, firstCard.Surface.InstanceId);
        Assert.AreEqual("1.20.1", firstCard.Surface.MinecraftVersion);
        await context.Launcher.NavigateAsync("instance", new Dictionary<string, string> { ["id"] = second });
        await Wait(() => firstCard.Disposed, "The old instance view was retained.");
        Assert.IsTrue(firstCard.Surface.Lifetime.IsCancellationRequested);
        Assert.AreEqual(second, mounts.Last(view => view.Region == ExtensionRegions.InstanceOverviewCards).Surface.InstanceId);
        await app.OpenExtensions();
        await Wait(() => mounts.Any(view => view.Region == ExtensionRegions.SettingsSections && view.IsEffectivelyVisible), "Settings section missing.");
        foreach (var area in Enum.GetValues<ExtensionPageArea>())
        {
            await context.Launcher.NavigateAsync(area == ExtensionPageArea.Settings ? "Setup" : area.ToString());
            await Wait(() => app.Window.GetVisualDescendants().OfType<PclListItem>().Any(item => item.Title == "Custom " + area && item.IsEffectivelyVisible), "Contextual page entry missing: " + area);
            var link = app.Window.GetVisualDescendants().OfType<PclListItem>().Single(item => item.Title == "Custom " + area);
            app.Screenshot("extension-navigation-" + area);
            await Click(app.Window, link);
            Assert.IsTrue(app.Launcher.HasExtensionPage);
            Assert.IsTrue(mounts.Last(view => view.Region == "Page " + area).IsEffectivelyVisible);
        }
        var pageBeforeLocale = mounts.Last(view => view.Region == "Page Tools");
        app.Text.SetLocale("zh-Hans"); await Settle();
        Assert.IsTrue(pageBeforeLocale.Disposed && pageBeforeLocale.Surface.Lifetime.IsCancellationRequested);
        app.Text.SetLocale("en-US");
        SeedCatalog(app.Launcher);
        await context.Launcher.NavigateAsync("Download/DownloadInstall"); await Settle();
        await Wait(() => app.Window.GetVisualDescendants().OfType<PclListItem>().Any(item => item.Title == "1.20.1" && item.IsEffectivelyVisible), "The installation catalog did not render after changing navigation areas.");
        await Click(app.Window, () => app.Window.GetVisualDescendants().OfType<PclListItem>().FirstOrDefault(item => item.Title == "1.20.1" && item.IsEffectivelyVisible));
        await Wait(() => mounts.Any(view => view.Region == ExtensionRegions.DownloadInstallExtras && view.IsEffectivelyVisible), "Installation extras were not mounted.");
        Assert.AreEqual("1.20.1", mounts.Last(view => view.Region == ExtensionRegions.DownloadInstallExtras).Surface.MinecraftVersion);
        await context.Launcher.NavigateAsync("Download/DownloadForge"); await Settle();
        await Wait(() => mounts.Any(view => view.Region == ExtensionRegions.DownloadCatalogExtras), "Manual catalog extras did not mount.");
        await context.Launcher.NavigateAsync("Download/DownloadMod"); await Settle();
        await Wait(() => mounts.Any(view => view.Region == ExtensionRegions.DownloadResourceExtras), "Resource catalog extras did not mount.");
        var resourceCard = mounts.Last(view => view.Region == ExtensionRegions.DownloadResourceExtras);
        Assert.AreEqual(ExtensionResourceKind.Mod, resourceCard.Surface.ResourceQuery?.Kind);
        app.Launcher.DownloadResourceSearchQuery = "captured resource query";
        await Wait(() => mounts.Last(view => view.Region == ExtensionRegions.DownloadResourceExtras).Surface.ResourceQuery?.Text == "captured resource query", "Resource filter changes did not update the contribution context.");
        Assert.IsTrue(resourceCard.Disposed && resourceCard.Surface.Lifetime.IsCancellationRequested);
        await context.Launcher.NavigateAsync("InstanceSetup/VersionMod"); await Settle();
        await Wait(() => mounts.Any(view => view.Region == ExtensionRegions.InstanceResourceExtras), "Instance resource extras did not mount.");
        await app.Session.Host.SetEnabledAsync(context.Manifest.Id, false); await Settle();
        Assert.IsTrue(mounts.All(view => view.Disposed && view.Surface.Lifetime.IsCancellationRequested));
        Assert.AreEqual(0, app.Launcher.ExtensionNavigationEntries.Count);
        Assert.IsFalse(app.Launcher.HasExtensionPage);

        TrackedView Track(string region, ExtensionSurfaceContext surface) { var view = new TrackedView(region, surface); mounts.Add(view); return view; }
    });

    [TestMethod]
    public Task ResourcePlan_ResolvesDependencies_InstallsVerifiedBytes_UpdatesInventory_AndReplacesOldVersions() => Run(async () =>
    {
        await using var server = new ExtensionTestRepository();
        await using var app = new DesktopFixture();
        var id = CreateInstance(app, "managed", "1.20.1");
        await app.Start();
        var provider = new ExtensionTestProvider();
        provider.Releases["dependency"] = [Release(server, "dependency", "dep-1.jar", "dependency bytes")];
        provider.Releases["main"] = [Release(server, "v1", "main-1.jar", "first version")];
        var context = await Provider(app, provider);
        var resources = context.GetService<IExtensionResources>();
        var instances = context.GetService<IExtensionInstances>();
        var first = await resources.PrepareInstallAsync(Request(id, "main"));
        Assert.IsFalse(File.Exists(Path.Combine(id, "mods", "main-1.jar")), "Preparing a review must not write game files.");
        await resources.ApplyInstallAsync(first.Id);
        Assert.AreEqual("first version", File.ReadAllText(Path.Combine(id, "mods", "main-1.jar")));
        provider.Releases["main"] = [Release(server, "v2", "main-2.jar", "second version") with { Dependencies = [Reference("dependency")] }];
        var upgrade = await resources.PrepareInstallAsync(Request(id, "main"));
        CollectionAssert.AreEqual(new[] { "dependency", "main" }, upgrade.Entries.Select(entry => entry.Resource.ProjectId).ToArray());
        CollectionAssert.AreEqual(new[] { Path.Combine(id, "mods", "main-1.jar") }, upgrade.Entries.Last().ReplacedPaths.ToArray());
        Assert.AreEqual("first version", File.ReadAllText(Path.Combine(id, "mods", "main-1.jar")));
        await context.GetService<IExtensionTasks>().RunAsync(new() { Id = "test.capabilities.install", Title = "Install managed resources" },
            async (progress, token) => await resources.ApplyInstallAsync(upgrade.Id, progress, token));
        Assert.IsFalse(File.Exists(Path.Combine(id, "mods", "main-1.jar")));
        Assert.AreEqual("second version", File.ReadAllText(Path.Combine(id, "mods", "main-2.jar")));
        Assert.AreEqual("dependency bytes", File.ReadAllText(Path.Combine(id, "mods", "dep-1.jar")));
        var inventory = await instances.GetAsync(id);
        Assert.AreEqual(2, inventory.Resources.Count(item => item.Kind == ExtensionResourceKind.Mod));
        Assert.IsTrue(inventory.Resources.All(item => item.ProviderId == "test.capabilities.repository" && item.ReleaseId is not null && item.Sha1?.Length == 40));
        await Wait(() => TaskCenter.Tasks.Any(task => task.Title == "Install managed resources" && task.State == TaskState.Success && task.Progress == 1), "Managed task did not complete.");
        await Assert.ThrowsAsync<ExtensionOperationException>(async () => await resources.ApplyInstallAsync(upgrade.Id));
        provider.Releases["main"] = [Release(server, "v3", "next.jar", "next version")];
        provider.Releases["overlap"] = [Release(server, "overlap", "main-2.jar", "overlapping destination")];
        var overlap = await Assert.ThrowsAsync<ExtensionOperationException>(async () => await resources.PrepareInstallAsync(new()
        { InstanceId = id, Resources = [Reference("main"), Reference("overlap")] }));
        Assert.AreEqual("resource.conflict", overlap.Code);
        Assert.AreEqual("second version", File.ReadAllText(Path.Combine(id, "mods", "main-2.jar")));
        Assert.IsFalse(File.Exists(Path.Combine(id, "mods", "next.jar")));
        Assert.IsFalse(Directory.EnumerateDirectories(id, ".pcl-extension-stage-*").Any());
        await context.Launcher.NavigateAsync("TaskManager"); await Settle(); app.Screenshot("extension-managed-install");
    });

    [TestMethod]
    public Task ResourceFailures_Checksum_StalePlan_Cycles_Conflicts_AndPathTraversalLeaveOriginalFilesIntact() => Run(async () =>
    {
        await using var server = new ExtensionTestRepository();
        await using var app = new DesktopFixture();
        var id = CreateInstance(app, "protected", "1.20.1");
        Directory.CreateDirectory(Path.Combine(id, "mods")); File.WriteAllText(Path.Combine(id, "mods", "original.jar"), "keep me");
        await app.Start();
        var provider = new ExtensionTestProvider();
        provider.Releases["main"] = [Release(server, "bad", "new.jar", "bad bytes") with { Sha256 = new string('0', 64) }];
        var context = await Provider(app, provider); var resources = context.GetService<IExtensionResources>();
        var plan = await resources.PrepareInstallAsync(Request(id, "main"));
        var error = await Assert.ThrowsAsync<ExtensionOperationException>(async () => await resources.ApplyInstallAsync(plan.Id));
        Assert.AreEqual("resource.checksum", error.Code);
        Assert.AreEqual("keep me", File.ReadAllText(Path.Combine(id, "mods", "original.jar")));
        Assert.IsFalse(File.Exists(Path.Combine(id, "mods", "new.jar")));
        provider.Releases["main"] = [Release(server, "good", "new.jar", "good bytes")];
        plan = await resources.PrepareInstallAsync(Request(id, "main"));
        File.WriteAllText(Path.Combine(id, "mods", "original.jar"), "changed after review");
        var before = server.Requests.Count;
        error = await Assert.ThrowsAsync<ExtensionOperationException>(async () => await resources.ApplyInstallAsync(plan.Id));
        Assert.AreEqual("plan.stale", error.Code); Assert.AreEqual(before, server.Requests.Count);
        provider.Releases["main"] = [provider.Releases["main"][0] with { FileName = "../outside.jar" }];
        await Assert.ThrowsAsync<InvalidDataException>(async () => await resources.PrepareInstallAsync(Request(id, "main")));
        provider.Releases["main"] = [Release(server, "cycle", "main.jar", "main") with { Dependencies = [Reference("dependency")] }];
        provider.Releases["dependency"] = [Release(server, "dep", "dependency.jar", "dep") with { Dependencies = [Reference("main")] }];
        error = await Assert.ThrowsAsync<ExtensionOperationException>(async () => await resources.PrepareInstallAsync(Request(id, "main")));
        Assert.AreEqual("resource.cycle", error.Code);
        provider.Releases["dependency"] = [Release(server, "dep", "dependency.jar", "dep")];
        var conflict = new ExtensionResourceInstallRequest { InstanceId = id, Resources = [Reference("main"), Reference("dependency") with { ReleaseId = "different" }] };
        error = await Assert.ThrowsAsync<ExtensionOperationException>(async () => await resources.PrepareInstallAsync(conflict));
        Assert.AreEqual("resource.conflict", error.Code);
        Assert.AreEqual("changed after review", File.ReadAllText(Path.Combine(id, "mods", "original.jar")));
        Assert.AreEqual(1, Directory.GetFiles(Path.Combine(id, "mods")).Length);
        Assert.IsFalse(Directory.EnumerateDirectories(id, ".pcl-extension-stage-*").Any());
        Assert.AreEqual(ExtensionStatus.Active, app.Session.Host.Get(context.Manifest.Id).Status);
    });

    [TestMethod]
    public Task TaskManagerCancel_AndDisableDuringDownload_CancelWorkAndRemoveStaging() => Run(async () =>
    {
        await using var server = new ExtensionTestRepository();
        await using var app = new DesktopFixture();
        var id = CreateInstance(app, "cancel", "1.20.1");
        await app.Start();
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var provider = new ExtensionTestProvider();
        provider.Releases["slow"] = [Release(server, "slow", "slow.jar", "slow", gate.Task)];
        var context = await Provider(app, provider); var resources = context.GetService<IExtensionResources>();
        var plan = await resources.PrepareInstallAsync(Request(id, "slow"));
        var operation = context.GetService<IExtensionTasks>().RunAsync(new() { Id = "test.capabilities.download", Title = "Cancelable extension download" },
            async (progress, token) => await resources.ApplyInstallAsync(plan.Id, progress, token)).AsTask();
        await Wait(() => server.Requests.Contains("/slow"), "The HTTP transfer never started.");
        await context.Launcher.NavigateAsync("TaskManager"); await Settle();
        var task = TaskCenter.Tasks.Single(task => task.Title == "Cancelable extension download");
        var taskCard = app.Window.GetVisualDescendants().OfType<PclCard>().Single(card => card.Header == "Cancelable extension download");
        await Click(app.Window, taskCard.Find<PclIconButton>("CloseButton")!);
        await Assert.ThrowsAsync<OperationCanceledException>(() => operation);
        await Wait(() => task.State == TaskState.Canceled, "Task cancellation was not presented.");
        Assert.IsFalse(File.Exists(Path.Combine(id, "mods", "slow.jar")));
        Assert.IsFalse(Directory.EnumerateDirectories(id, ".pcl-extension-stage-*").Any());
        plan = await resources.PrepareInstallAsync(Request(id, "slow"));
        var second = resources.ApplyInstallAsync(plan.Id).AsTask();
        await Wait(() => server.Requests.Count(path => path == "/slow") == 2, "Second transfer did not start.");
        await app.Session.Host.SetEnabledAsync(context.Manifest.Id, false);
        await Assert.ThrowsAsync<OperationCanceledException>(() => second);
        Assert.IsFalse(File.Exists(Path.Combine(id, "mods", "slow.jar")));
        Assert.IsFalse(Directory.EnumerateDirectories(id, ".pcl-extension-stage-*").Any());
        gate.SetResult();
    });

    [TestMethod]
    public Task Clone_CreateFromCustomManifest_AndRecoverableRemovalPreserveSourceAndGameData() => Run(async () =>
    {
        await using var server = new ExtensionTestRepository();
        await using var app = new DesktopFixture();
        var source = CreateInstance(app, "source", "1.20.1");
        Directory.CreateDirectory(Path.Combine(source, "saves", "world")); File.WriteAllText(Path.Combine(source, "saves", "world", "level.dat"), "original world");
        Directory.CreateDirectory(Path.Combine(source, "mods")); File.WriteAllText(Path.Combine(source, "mods", "old.jar"), "source mod");
        await app.Start();
        var context = await Activate(app); var instances = context.GetService<IExtensionInstances>();
        var provider = new ExtensionTestProvider();
        provider.Releases["migrating-mod"] = [Release(server, "target-mod", "target.jar", "target version") with { MinecraftVersions = ["1.21.1"] },
            Release(server, "source-mod", "source.jar", "source version")];
        context.GetService<IExtensionResourceProviders>().Register(new() { Id = "test.capabilities.repository", Title = "Migration source", Kinds = [ExtensionResourceKind.Mod] }, provider);
        var resources = context.GetService<IExtensionResources>();
        var initial = await resources.PrepareInstallAsync(Request(source, "migrating-mod")); await resources.ApplyInstallAsync(initial.Id);
        var clone = await instances.CloneAsync(new() { SourceInstanceId = source, Name = "clone" });
        Assert.AreEqual("original world", File.ReadAllText(Path.Combine(clone.Directory, "saves", "world", "level.dat")));
        Assert.AreEqual("source mod", File.ReadAllText(Path.Combine(clone.Directory, "mods", "old.jar")));
        Assert.AreEqual("clone", JsonNode.Parse(File.ReadAllText(Path.Combine(clone.Directory, "clone.json")))!["id"]!.GetValue<string>());
        File.WriteAllText(Path.Combine(clone.Directory, "mods", "old.jar"), "clone mod");
        Assert.AreEqual("source mod", File.ReadAllText(Path.Combine(source, "mods", "old.jar")));
        await Assert.ThrowsAsync<ExtensionOperationException>(async () => await instances.CloneAsync(new() { SourceInstanceId = source, Name = "clone" }));
        await Assert.ThrowsAsync<ArgumentException>(async () => await instances.CloneAsync(new() { SourceInstanceId = source, Name = "../escape" }));
        var jar = Encoding.UTF8.GetBytes("downloaded client bytes");
        var client = server.Add("/client.jar", jar);
        var manifest = server.AddJson("/version.json", new { id = "1.21.1", clientVersion = "1.21.1", type = "release", mainClass = "net.minecraft.client.main.Main",
            libraries = Array.Empty<object>(), downloads = new { client = new { url = client, sha1 = Convert.ToHexString(SHA1.HashData(jar)).ToLowerInvariant(), size = jar.Length } } });
        var created = await instances.CreateAsync(new() { Name = "migrated", MinecraftVersion = "1.21.1", MinecraftManifestUrl = manifest });
        CollectionAssert.AreEqual(jar, File.ReadAllBytes(Path.Combine(created.Directory, "migrated.jar")));
        Assert.AreEqual("1.21.1", (await instances.GetAsync(created.Id)).Instance.MinecraftVersion);
        var sourceResources = (await instances.GetAsync(source)).Resources.Where(item => item.Kind == ExtensionResourceKind.Mod && item.ProjectId is not null)
            .Select(item => new ExtensionResourceReference { ProviderId = item.ProviderId!, ProjectId = item.ProjectId! }).ToArray();
        var migration = await resources.PrepareInstallAsync(new() { InstanceId = created.Id, Resources = sourceResources });
        Assert.AreEqual("target-mod", migration.Entries.Single().Release.Id);
        Assert.IsFalse(File.Exists(Path.Combine(created.Directory, "mods", "target.jar")));
        await resources.ApplyInstallAsync(migration.Id);
        Assert.AreEqual("target version", File.ReadAllText(Path.Combine(created.Directory, "mods", "target.jar")));
        Assert.AreEqual("source version", File.ReadAllText(Path.Combine(source, "mods", "source.jar")));
        Assert.IsFalse(File.Exists(Path.Combine(created.Directory, "mods", "old.jar")), "Unidentified mods must remain explicit decisions for the migration extension.");
        var retained = await instances.RemoveAsync(clone.Id);
        Assert.IsFalse(Directory.Exists(clone.Directory));
        Assert.AreEqual("clone mod", File.ReadAllText(Path.Combine(retained, "mods", "old.jar")));
        Assert.IsTrue(Directory.Exists(source));
        Assert.IsFalse((await context.Launcher.GetInstancesAsync()).Any(item => item.Id == clone.Id));
        Assert.IsFalse(Directory.EnumerateDirectories(Path.GetDirectoryName(source)!, ".pcl-clone-*").Any());
    });

    [TestMethod]
    public Task LegacyBinaryLoadsAgainstCurrentSdk_AndUnsupportedCapabilitiesAreRejectedBeforeActivation() => Run(async () =>
    {
        await using var app = new DesktopFixture(); await app.Start();
        var path = Path.Combine(app.Root, "legacy.pclext");
        var manifest = new ExtensionManifest { Id = "example.hello", Name = "Legacy extension", Version = "1.0.0", AvaloniaMajorVersion = 11,
            EntryAssembly = "LegacyExtension.dll", EntryType = "LegacyExtension.LegacyExtension", Commands = [new() { Id = "example.hello.open", Title = "Hello" }] };
        using (var zip = ZipFile.Open(path, ZipArchiveMode.Create))
        {
            using (var stream = zip.CreateEntry("extension.json").Open()) JsonSerializer.Serialize(stream, manifest, ExtensionPackage.JsonOptions);
            zip.CreateEntryFromFile(Path.Combine(AppContext.BaseDirectory, "Compatibility", "LegacyExtension.dll"), "LegacyExtension.dll");
        }
        var info = await app.Session.Host.InstallAsync(path); await app.Session.Host.SetEnabledAsync(info.Manifest.Id, true);
        await app.Session.Host.ExecuteCommandAsync("example.hello.open"); await Settle();
        Assert.IsTrue(app.Window.Find<ContentControl>("ExtensionWorkspace")!.IsVisible);
        Assert.IsTrue(app.Window.GetVisualDescendants().OfType<TextBlock>().Any(text => text.Text == "Hello from an extension!"));
        Assert.IsTrue((await app.Session.Host.QueryAsync(new("hello"))).Any(result => result.CommandId == "example.hello.open"));
        var activated = false;
        Assert.Throws<NotSupportedException>(() => app.Session.Host.AddBuiltIn(new() { Id = "test.unsupported", Name = "Unsupported", Version = "1.0.0", RequiredCapabilities = ["pcl.future"] }, app.Root,
            () => new ProbeExtension(_ => { activated = true; return ValueTask.CompletedTask; })));
        Assert.IsFalse(activated);
        Assert.Throws<NotSupportedException>(() => app.Session.Host.AddBuiltIn(new() { Id = "test.newer", Name = "Newer", Version = "1.0.0", MinimumSdkVersion = "99.0.0" }, app.Root, () => new ProbeExtension(_ => ValueTask.CompletedTask)));
        var context = await Activate(app);
        Assert.IsTrue(context.GetService<IExtensionCapabilities>().Supports(ExtensionCapabilities.Resources));
        Assert.IsFalse(context.GetService<IExtensionCapabilities>().Supports("pcl.future"));
        await app.Session.Host.SetEnabledAsync(info.Manifest.Id, false); await Settle();
        Assert.IsFalse(app.Window.Find<ContentControl>("ExtensionWorkspace")!.IsVisible);
    });

    private static readonly string[] Regions = [ExtensionRegions.DownloadInstallExtras, ExtensionRegions.DownloadCatalogExtras, ExtensionRegions.DownloadResourceExtras,
        ExtensionRegions.InstanceOverviewCards, ExtensionRegions.InstanceResourceExtras, ExtensionRegions.SettingsSections];
    private sealed class TrackedView : TextBlock, IDisposable
    {
        public string Region { get; }
        public ExtensionSurfaceContext Surface { get; }
        public TrackedView(string region, ExtensionSurfaceContext surface) { Region = region; Surface = surface; Text = region; }
        public bool Disposed { get; private set; }
        public void Dispose() => Disposed = true;
    }
    private sealed class ProbeExtension(Func<IExtensionContext, ValueTask> activate) : IExtension
    {
        public ValueTask ActivateAsync(IExtensionContext context, CancellationToken token) => activate(context);
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
    private static async Task<IExtensionContext> Activate(DesktopFixture app, Func<IExtensionContext, ValueTask>? activate = null, string id = "test.capabilities")
    {
        IExtensionContext? context = null;
        app.Session.Host.AddBuiltIn(new() { Id = id, Name = id, Version = "1.0.0" }, app.Root, () => new ProbeExtension(value =>
        { context = value; return activate?.Invoke(value) ?? ValueTask.CompletedTask; }));
        await app.Session.Host.ActivateAsync(id); await Settle(); return context!;
    }
    private static Task<IExtensionContext> Provider(DesktopFixture app, ExtensionTestProvider provider) => Activate(app, context =>
    {
        context.GetService<IExtensionResourceProviders>().Register(new() { Id = "test.capabilities.repository", Title = "Test repository", Kinds = Enum.GetValues<ExtensionResourceKind>() }, provider);
        return ValueTask.CompletedTask;
    });
    private static ExtensionResourceReference Reference(string id) => new() { ProviderId = "test.capabilities.repository", ProjectId = id };
    private static ExtensionResourceInstallRequest Request(string id, params string[] projects) => new() { InstanceId = id, Resources = projects.Select(Reference).ToArray() };
    private static ExtensionResourceRelease Release(ExtensionTestRepository server, string id, string file, string content, Task? gate = null)
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        return new() { Id = id, Title = id, FileName = file, DownloadUrl = server.Add("/" + id, bytes, gate), Kind = ExtensionResourceKind.Mod,
            MinecraftVersions = ["1.20.1"], Sha256 = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant() };
    }
    private static string CreateInstance(DesktopFixture app, string name, string version)
    {
        var path = app.CreateInstance(name);
        var manifest = Path.Combine(path, name + ".json");
        var json = JsonNode.Parse(File.ReadAllText(manifest))!; json["clientVersion"] = version;
        File.WriteAllText(manifest, json.ToJsonString());
        var config = FrontendRuntimePaths.OpenInstanceConfigProvider(path); config.Set("VersionArgumentIndieV2", true); config.Sync();
        return path;
    }
    private static void SeedCatalog(LauncherViewModel launcher, string? manifestUrl = null)
    {
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        void Set(string name, object value) => typeof(LauncherViewModel).GetField(name, flags)!.SetValue(launcher, value);
        // Ignore a live catalog request started before this deterministic fixture
        // was installed. Its late completion must not replace the local manifest.
        var version = typeof(LauncherViewModel).GetField("_downloadInstallMinecraftCatalogVersion", flags)!;
        version.SetValue(launcher, (int)version.GetValue(launcher)! + 1);
        Set("_downloadInstallMinecraftCatalogLoaded", true); Set("_isDownloadInstallMinecraftCatalogLoading", false);
        Set("_downloadInstallMinecraftCatalogChoices", new[] { new FrontendInstallChoice("minecraft:1.20.1", "1.20.1", "Test version", "1.20.1", FrontendInstallChoiceKind.Minecraft, ManifestUrl: manifestUrl,
            Metadata: new JsonObject { ["catalogGroup"] = "release" }) });
    }
}
