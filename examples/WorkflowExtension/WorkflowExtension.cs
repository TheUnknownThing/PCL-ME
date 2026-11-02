using System.IO.Compression;
using System.Text.Json;
using Avalonia.Controls;
using PCL.Extensions.Avalonia;
using PCL.Extensions.Contracts;
using PCL.UI.Commands;
using PCL.UI.Controls;

namespace WorkflowExtension;

public sealed class WorkflowExtension : IExtension
{
    private sealed record PendingPreset(string Version, string? Loader, ExtensionResourceReference[] Resources);
    private PendingPreset? _pending;
    private bool _includeRemembered;
    private readonly object _sync = new();

    public ValueTask ActivateAsync(IExtensionContext context, CancellationToken token)
    {
        var contributions = context.GetService<IExtensionContributions>();
        var repository = context.Own(new RepositoryProvider(context));
        context.GetService<IExtensionResourceProviders>().Register(new()
        { Id = "example.workflows.catalog", Title = context.Text.Get("catalog.title"), Kinds = Enum.GetValues<ExtensionResourceKind>() }, repository);
        contributions.RegisterPage(new() { Id = "example.workflows.skins", Title = "Skins", TitleKey = "skins.title", Area = ExtensionPageArea.Download }, surface => SkinBrowser(context, surface));
        contributions.RegisterPage(new() { Id = "example.workflows.instances", Title = "Instance tools", TitleKey = "instances.title", Area = ExtensionPageArea.Tools }, surface => InstanceTools(context, surface));
        contributions.RegisterSection(new() { Id = "example.workflows.preset", Title = "Common mods", TitleKey = "preset.title", Region = ExtensionRegions.DownloadInstallExtras }, surface => Preset(context, surface));
        contributions.RegisterAction(new() { Id = "example.workflows.instance-tools", Title = "Instance tools", TitleKey = "instances.title", Location = ExtensionContextActionLocation.Instance },
            async (target, cancellation) =>
            {
                await context.Launcher.NavigateAsync("instance", new Dictionary<string, string> { ["id"] = target.InstanceId }, cancellation);
                await contributions.ShowPageAsync("example.workflows.instances", cancellation);
            });
        contributions.RegisterAction(new() { Id = "example.workflows.world-backup", Title = "Back up world", TitleKey = "backup.title", Location = ExtensionContextActionLocation.World },
            async (target, cancellation) => await context.GetService<IExtensionTasks>().RunAsync(new() { Id = "example.workflows.backup", Title = context.Text.Get("backup.title") }, (_, operationToken) =>
            {
                var source = target.ResourcePath ?? throw new InvalidOperationException(context.Text.Get("instance.required"));
                var backupDirectory = Path.Combine(context.DataDirectory, "Backups"); Directory.CreateDirectory(backupDirectory);
                var destination = Path.Combine(backupDirectory, DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N") + ".zip");
                try
                {
                    using var archive = ZipFile.Open(destination, ZipArchiveMode.Create);
                    AddWorld(archive, source, source, operationToken);
                }
                catch { File.Delete(destination); throw; }
                return Task.CompletedTask;
            }, cancellation));
        context.Events.Subscribe(ExtensionTopics.InstanceInstalled, message => context.RunBackground(async cancellation =>
        {
            PendingPreset? pending;
            lock (_sync) pending = _pending;
            if (pending is null) return;
            var installed = message.Data.Deserialize<ExtensionLifecycleEvent>()!;
            if (installed.IsNewInstance != true) return;
            var id = installed.InstanceId;
            var instance = await context.GetService<IExtensionInstances>().GetAsync(id, cancellation);
            if (instance.Instance.MinecraftVersion != pending.Version || instance.Loader != pending.Loader) return;
            lock (_sync) { if (!ReferenceEquals(_pending, pending)) return; _pending = null; }
            try
            {
                await context.GetService<IExtensionTasks>().RunAsync(new() { Id = "example.workflows.preset-install", Title = context.Text.Get("preset.title") }, async (progress, operationToken) =>
                {
                    var resources = context.GetService<IExtensionResources>();
                    var plan = await resources.PrepareInstallAsync(new() { InstanceId = id, Resources = pending.Resources }, operationToken);
                    await resources.ApplyInstallAsync(plan.Id, progress, operationToken);
                }, cancellation);
            }
            catch (ExtensionOperationException ex) { context.Log(ExtensionLogLevel.Warning, ex.Message); context.Launcher.Notify(ex.Message, true); }
        }));
        context.Events.Subscribe(ExtensionTopics.ResourcesChanged, message =>
        {
            if (!context.Settings.Get("recordProjects", false)) return;
            context.RunBackground(async cancellation =>
            {
                try
                {
                    var instance = await context.GetService<IExtensionInstances>().GetAsync(message.Data.Deserialize<ExtensionLifecycleEvent>()!.InstanceId, cancellation);
                    context.Settings.Set("recentProjects", instance.Resources.Where(resource => resource.Kind == ExtensionResourceKind.Mod && resource.ProjectId is not null)
                        .Select(resource => new ExtensionResourceReference { ProviderId = resource.ProviderId!, ProjectId = resource.ProjectId! }).ToArray());
                }
                catch (ExtensionOperationException ex) { context.Log(ExtensionLogLevel.Warning, ex.Message); }
            });
        });
        context.GetService<IExtensionLaunchHooks>().Register(new() { Id = "example.workflows.launch-property", FailurePolicy = ExtensionHookFailurePolicy.ContinueLaunch }, (_, _) =>
            ValueTask.FromResult(new ExtensionLaunchChanges { JvmArguments = context.Settings.Get("launchProperty", false) ? ["-Dpcl.example.workflows=true"] : [] }));
        return ValueTask.CompletedTask;
    }

    private static WorkflowPanel SkinBrowser(IExtensionContext context, ExtensionSurfaceContext surface)
    {
        var panel = new WorkflowPanel(context, surface.Lifetime);
        var query = new PclSearchBox { Name = "ExampleSkinQuery", Watermark = context.Text.Get("skins.search") };
        var results = new StackPanel { Spacing = 10 };
        panel.Body.Children.Add(query);
        panel.AddButton("ExampleSkinSearch", "search", async () =>
        {
            var resources = context.GetService<IExtensionResources>();
            var page = await resources.SearchAsync(new() { ProviderId = "example.workflows.catalog", Kind = ExtensionResourceKind.Skin, Text = query.Text ?? "" }, panel.Lifetime);
            results.Children.Clear();
            foreach (var project in page.Projects)
            {
                var button = new PclButton { Text = project.Title, Name = "ExampleSkin_" + project.ProjectId };
                button.Command = new ActionCommand(() => panel.Run(async () =>
                {
                    if (surface.InstanceId is null) throw new InvalidOperationException(context.Text.Get("instance.required"));
                    await Review(panel, context, new() { InstanceId = surface.InstanceId, Resources = [new() { ProviderId = project.ProviderId, ProjectId = project.ProjectId }] });
                }));
                results.Children.Add(button);
            }
            if (page.Projects.Count == 0) panel.Feedback.Text = context.Text.Get("catalog.empty");
        });
        panel.Body.Children.Add(results);
        return panel;
    }

    private WorkflowPanel Preset(IExtensionContext context, ExtensionSurfaceContext surface)
    {
        var panel = new WorkflowPanel(context, surface.Lifetime, scroll: false);
        var recent = context.Settings.Get("recentProjects", Array.Empty<ExtensionResourceReference>());
        var remembered = new CheckBox { Name = "ExampleRememberedMods", Content = context.Text.Get("preset.remembered"), IsVisible = recent.Length > 0 };
        ExtensionResourceReference[] References() => context.Settings.Get("presetProjects", "sodium,lithium")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(id => new ExtensionResourceReference { ProviderId = context.Settings.Get("presetProvider", "pcl.modrinth"), ProjectId = id })
            .Concat(remembered.IsChecked == true ? recent : [])
            .DistinctBy(item => (item.ProviderId, item.ProjectId)).ToArray();
        var next = new CheckBox { Content = context.Text.Get("preset.next") };
        lock (_sync)
        {
            next.IsChecked = _pending is not null && _pending.Version == surface.MinecraftVersion && _pending.Loader == surface.Loader;
            remembered.IsChecked = _includeRemembered;
        }
        next.IsEnabled = References().Length > 0;
        void UpdatePending() { lock (_sync) _pending = next.IsChecked == true && surface.MinecraftVersion is { } version ? new(version, surface.Loader, References()) : null; }
        next.IsCheckedChanged += (_, _) => UpdatePending();
        remembered.IsCheckedChanged += (_, _) =>
        {
            lock (_sync) _includeRemembered = remembered.IsChecked == true;
            next.IsEnabled = References().Length > 0;
            if (!next.IsEnabled) next.IsChecked = false;
            UpdatePending();
        };
        panel.Body.Children.Add(remembered);
        panel.Body.Children.Add(next);
        panel.AddButton("ExamplePresetReview", "review", async () =>
        {
            if (surface.InstanceId is null) throw new InvalidOperationException(context.Text.Get("instance.required"));
            await Review(panel, context, new() { InstanceId = surface.InstanceId, Resources = References() });
        });
        return panel;
    }

    private static async Task Review(WorkflowPanel panel, IExtensionContext context, ExtensionResourceInstallRequest request)
    {
        var resources = context.GetService<IExtensionResources>();
        var plan = await resources.PrepareInstallAsync(request, panel.Lifetime);
        panel.Lifetime.ThrowIfCancellationRequested();
        var preview = new StackPanel { Spacing = 8 };
        foreach (var entry in plan.Entries) preview.Children.Add(new TextBlock { Text = entry.Release.Title + " → " + Path.GetFileName(entry.Destination) });
        var install = new PclButton { Name = "ExampleApplyInstall", Text = context.Text.Get("install") };
        install.Command = new ActionCommand(() => panel.Run(async () =>
        {
            install.IsEnabled = false;
            await context.GetService<IExtensionTasks>().RunAsync(new() { Id = "example.workflows.install", Title = context.Text.Get("install") },
                async (progress, token) => await resources.ApplyInstallAsync(plan.Id, progress, token), panel.Lifetime);
            panel.Feedback.Text = context.Text.Get("done");
        }));
        preview.Children.Add(install); panel.Body.Children.Add(preview);
    }

    private static WorkflowPanel InstanceTools(IExtensionContext context, ExtensionSurfaceContext surface)
    {
        var panel = new WorkflowPanel(context, surface.Lifetime);
        var name = new TextBox { Name = "ExampleInstanceName", Watermark = context.Text.Get("instances.name") };
        var version = new TextBox { Name = "ExampleMinecraftVersion", Watermark = context.Text.Get("instances.version") };
        panel.Body.Children.Add(name); panel.Body.Children.Add(version);
        panel.AddButton("ExampleCloneInstance", "instances.clone", async () =>
        {
            if (surface.InstanceId is null) throw new InvalidOperationException(context.Text.Get("instance.required"));
            var targetName = name.Text ?? "";
            await context.GetService<IExtensionTasks>().RunAsync(new() { Id = "example.workflows.clone", Title = context.Text.Get("instances.clone") }, async (_, token) =>
                await context.GetService<IExtensionInstances>().CloneAsync(new() { SourceInstanceId = surface.InstanceId, Name = targetName }, token), panel.Lifetime);
            panel.Feedback.Text = context.Text.Get("done");
        });
        panel.AddButton("ExampleCreateInstance", "instances.create", async () =>
        {
            var targetName = name.Text ?? "";
            var minecraftVersion = version.Text ?? "";
            await context.GetService<IExtensionTasks>().RunAsync(new() { Id = "example.workflows.create", Title = context.Text.Get("instances.create") }, async (_, token) =>
                await context.GetService<IExtensionInstances>().CreateAsync(new() { Name = targetName, MinecraftVersion = minecraftVersion }, token), panel.Lifetime);
            panel.Feedback.Text = context.Text.Get("done");
        });
        return panel;
    }

    private static void AddWorld(ZipArchive archive, string root, string directory, CancellationToken token)
    {
        if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0) throw new IOException("Symbolic links are not supported in world backups.");
        foreach (var entry in Directory.EnumerateFileSystemEntries(directory))
        {
            token.ThrowIfCancellationRequested();
            if ((File.GetAttributes(entry) & FileAttributes.ReparsePoint) != 0) throw new IOException("Symbolic links are not supported in world backups.");
            if (Directory.Exists(entry)) AddWorld(archive, root, entry, token);
            else archive.CreateEntryFromFile(entry, Path.GetRelativePath(root, entry).Replace('\\', '/'));
        }
    }
    public ValueTask DisposeAsync() { lock (_sync) { _pending = null; _includeRemembered = false; } return ValueTask.CompletedTask; }

    private sealed class WorkflowPanel : ContentControl, IDisposable
    {
        private readonly IExtensionContext _context;
        private readonly CancellationTokenSource _lifetime;
        public CancellationToken Lifetime { get; }
        public StackPanel Body { get; } = new() { Margin = new(20), Spacing = 12 };
        public TextBlock Feedback { get; } = new() { Name = "ExampleFeedback", TextWrapping = global::Avalonia.Media.TextWrapping.Wrap };
        public WorkflowPanel(IExtensionContext context, CancellationToken lifetime, bool scroll = true)
        {
            _context = context; _lifetime = CancellationTokenSource.CreateLinkedTokenSource(context.Lifetime, lifetime); Lifetime = _lifetime.Token;
            Content = scroll ? new ScrollViewer { Content = Body } : Body; Body.Children.Add(Feedback);
        }
        public void AddButton(string name, string key, Func<Task> execute) => Body.Children.Add(new PclButton
        { Name = name, Text = _context.Text.Get(key), Command = new ActionCommand(() => Run(execute)) });
        public async void Run(Func<Task> operation)
        {
            try { await operation(); }
            catch (OperationCanceledException) when (Lifetime.IsCancellationRequested) { }
            catch (Exception ex) { if (!Lifetime.IsCancellationRequested) Feedback.Text = ex.Message; }
        }
        public void Dispose() { _lifetime.Cancel(); _lifetime.Dispose(); }
    }
}
