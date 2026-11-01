using PCL.Extensions.Contracts;
using PCL.Extensions.Runtime;
using PCL.Frontend.Avalonia.Workflows;

namespace PCL.Frontend.Avalonia.Desktop.Extensions;

internal sealed partial class FrontendExtensionSession
{
    private IReadOnlyList<LauncherLocation> _locations = [];

    private void RegisterHostServices()
    {
        _locations = _launcher.GetExtensionLocations();
        _hostRegistrations.Add(Host.RegisterHostCommand(new() { Id = "pcl.launcher.show", Title = _text.T("desktop.actions.show_launcher"), TitleKey = "desktop.actions.show_launcher" }, _ =>
        { CloseWorkspace(); _shell.ShowLauncher(); return ValueTask.CompletedTask; }));
        _hostRegistrations.Add(Host.RegisterHostCommand(new() { Id = "pcl.launcher.launch-selected", Title = _text.T("desktop.actions.launch_selected"), TitleKey = "desktop.actions.launch_selected" }, _ =>
        { CloseWorkspace(); _shell.LaunchSelectedInstance(); return ValueTask.CompletedTask; }, () => CanInteract && _launcher.LaunchCommand.CanExecute(null)));
        _hostRegistrations.Add(Host.RegisterHostCommand(new() { Id = "pcl.extensions.manage", Title = T("title"), TitleKey = "extensions.title" }, _ =>
        { ShowManager(); return ValueTask.CompletedTask; }, () => CanInteract));
        _hostRegistrations.Add(Host.RegisterHostCommand(new() { Id = "pcl.launcher.quit", Title = _text.T("desktop.actions.quit"), TitleKey = "desktop.actions.quit" }, _ =>
        { _shell.Quit(); return ValueTask.CompletedTask; }));
        _hostRegistrations.Add(Host.RegisterHostSearch("pcl.launcher.commands", async (query, token) =>
        {
            IReadOnlyList<ExtensionSearchResult> result = [];
            await DispatchAsync(() =>
            {
                result = Host.Commands.Where(command => command.CanExecute && command.Definition.Id != "pcl.global-search.open" && command.Definition.Id != "pcl.extensions.manage").Select(command =>
                {
                    var title = command.Definition.TitleKey is { } key
                        ? command.OwnerId is { } owner ? Host.Get(owner).Text.Get(key, command.Definition.Title) : _text.T(key)
                        : command.Definition.Title;
                    return (command, title);
                }).Where(pair => Matches(pair.title + " " + string.Join(' ', pair.command.Definition.Keywords), query.Text))
                    .Select(pair => new ExtensionSearchResult
                    {
                        Id = pair.command.Definition.Id, Title = pair.title,
                        Description = pair.command.OwnerId is { } owner ? Host.Get(owner).Text.Get("extension.name", Host.Get(owner).Manifest.Name) : _launcher.Title,
                        Shortcut = pair.command.Definition.Shortcut, CommandId = pair.command.Definition.Id, Kind = ExtensionSearchKind.Command, Score = .8
                    }).ToArray();
                return ValueTask.CompletedTask;
            });
            return result;
        }));
        _hostRegistrations.Add(Host.RegisterHostSearch("pcl.launcher.locations", (query, token) =>
        {
            var result = GetLocations().Select((location, index) => (location, index))
                .Where(pair => (query.Text.Length > 0 || pair.location.Kind is not (ExtensionSearchKind.Setting or ExtensionSearchKind.Help))
                    && Matches(pair.location.Title + " " + pair.location.Description + " " + pair.location.Keywords, query.Text))
                .Select(pair => new ExtensionSearchResult
                {
                    Id = pair.location.Route + ":" + pair.index, Title = pair.location.Title,
                    Description = pair.location.Description, Route = pair.location.Route, Kind = pair.location.Kind,
                    IconData = pair.location.Route == "Tools/ToolsExtensions" ? PCL.UI.Icons.PclIcons.Extensions.Data : null, Score = .5
                }).ToList();
            if (query.Text.Length is > 0 and <= 64 && System.Text.RegularExpressions.Regex.IsMatch(query.Text, @"^(?:\d|(?:rd|inf)-|[ab]\d)[A-Za-z0-9._+-]*$", System.Text.RegularExpressions.RegexOptions.CultureInvariant))
                result.Add(new() { Id = "minecraft-version", Kind = ExtensionSearchKind.Page, Title = T("search_version") + " " + query.Text, Route = "Download/DownloadInstall", Arguments = new Dictionary<string, string> { ["query"] = query.Text } });
            return ValueTask.FromResult<IReadOnlyList<ExtensionSearchResult>>(result);
        }));
        _hostRegistrations.Add(Host.RegisterHostSearch("pcl.launcher.instances", async (query, token) =>
            (await GetInstancesAsync(token)).Where(instance => Matches(instance.Name + " " + instance.MinecraftVersion, query.Text))
            .Select(instance => new ExtensionSearchResult
            {
                Id = instance.Id, Title = instance.Name, Description = instance.MinecraftVersion is { Length: > 0 } version ? "Minecraft " + version : "Minecraft", Kind = ExtensionSearchKind.Instance,
                Route = "instance", Arguments = new Dictionary<string, string> { ["id"] = instance.Id }, Score = .9
            }).ToArray()));
    }

    private static bool Matches(string text, string query) => query.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .All(term => text.Contains(term, StringComparison.CurrentCultureIgnoreCase));

    public async ValueTask<IReadOnlyList<LauncherInstance>> GetInstancesAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<LauncherInstance> result = [];
        await DispatchAsync(async () => result = await _launcher.GetExtensionInstancesAsync(cancellationToken));
        return result;
    }
    public IReadOnlyList<LauncherLocation> GetLocations() => _locations.Concat(_contributionLocations).ToArray();
    internal void RefreshLocations() => _locations = _launcher.GetExtensionLocations();

    public ValueTask NavigateAsync(string route, IReadOnlyDictionary<string, string>? arguments = null, CancellationToken cancellationToken = default) => DispatchAsync(async () =>
    {
        if (!CanInteract) throw new ExtensionOperationException("launcher.busy", T("unavailable"));
        CloseWorkspace();
        _shell.ShowLauncher();
        if (route.StartsWith("extension-page/", StringComparison.Ordinal))
        {
            var id = route["extension-page/".Length..];
            var page = _pages.GetValueOrDefault(id) ?? throw new ExtensionOperationException("page.unavailable", T("unavailable"));
            await _launcher.NavigateExtensionAsync(PageAreaRoute(page.Definition.Area), null, cancellationToken);
            ShowContributionPage(id);
            return;
        }
        await _launcher.NavigateExtensionAsync(route, arguments, cancellationToken);
    });

    public ValueTask LaunchAsync(string instanceId, CancellationToken cancellationToken = default) => DispatchAsync(async () =>
    {
        if (!CanInteract) throw new ExtensionOperationException("launcher.busy", T("unavailable"));
        CloseWorkspace();
        _shell.ShowLauncher();
        await _launcher.LaunchExtensionInstanceAsync(instanceId, cancellationToken);
    });

    public void ShowLauncher() => global::Avalonia.Threading.Dispatcher.UIThread.Post(_shell.ShowLauncher);
    public void Notify(string message, bool isError = false) => global::Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        AvaloniaHintBus.Show(message, isError ? AvaloniaHintTheme.Error : AvaloniaHintTheme.Info));

    public async ValueTask<string?> PickFileAsync(string title, IReadOnlyList<string> patterns, CancellationToken cancellationToken = default)
    {
        string? result = null;
        await DispatchAsync(async () =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            result = await _actions.PickOpenFileAsync(title, title, patterns.ToArray());
        });
        cancellationToken.ThrowIfCancellationRequested();
        return result;
    }
}
