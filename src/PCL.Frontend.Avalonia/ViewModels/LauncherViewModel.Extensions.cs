using PCL.Core.App.Essentials;
using PCL.Extensions.Contracts;
using PCL.Frontend.Avalonia.Desktop.Extensions;
using PCL.Frontend.Avalonia.Workflows;

namespace PCL.Frontend.Avalonia.ViewModels;

internal sealed partial class LauncherViewModel
{
    internal FrontendExtensionSession? ExtensionSession { get; set; }
    internal void OpenExtensionManager() => NavigateTo(new(LauncherFrontendPageKey.Tools, LauncherFrontendSubpageKey.ToolsExtensions), "Extensions");
    private object? _extensionInstanceTabsContent;
    public object? ExtensionInstanceTabsContent => _extensionInstanceTabsContent;
    internal void SetExtensionInstanceTabs(object? content) => SetProperty(ref _extensionInstanceTabsContent, content, nameof(ExtensionInstanceTabsContent));
    internal string? ExtensionSelectedInstanceId => _instanceComposition.Selection.HasSelection ? _instanceComposition.Selection.InstanceDirectory : null;

    internal async ValueTask<IReadOnlyList<LauncherInstance>> GetExtensionInstancesAsync(CancellationToken cancellationToken)
    {
        var folders = GetExtensionLauncherFolders();
        var selected = _instanceComposition.Selection.InstanceDirectory;
        return await Task.Run<IReadOnlyList<LauncherInstance>>(() =>
        {
            var instances = new List<LauncherInstance>();
            foreach (var folder in folders)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var versions = Path.Combine(folder, "versions");
                if (!Directory.Exists(versions)) continue;
                foreach (var path in Directory.EnumerateDirectories(versions))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (Path.GetFileName(path).StartsWith(".pcl-", StringComparison.Ordinal)) continue;
                    if (!FrontendRuntimePaths.IsRecognizedInstanceDirectory(path)) continue;
                    var version = FrontendVersionManifestInspector.ReadProfile(folder, Path.GetFileName(path)).VanillaVersion;
                    instances.Add(new(path, Path.GetFileName(path), path, version, string.Equals(path, selected, GetPathComparison())));
                }
            }
            return instances;
        }, cancellationToken);
    }

    internal string[] GetExtensionLauncherFolders()
    {
        var paths = _launcherActionService.RuntimePaths;
        return LoadConfiguredInstanceSelectionFolders(paths.OpenSharedConfigProvider(), paths.OpenLocalConfigProvider(), paths)
            .Select(folder => folder.Directory).Append(_instanceComposition.Selection.LauncherDirectory).Distinct(GetPathComparer()).ToArray();
    }

    internal IReadOnlyList<LauncherLocation> GetExtensionLocations()
    {
        var locations = new List<LauncherLocation>();
        foreach (var page in LauncherFrontendNavigationService.GetCatalog().TopLevelPages)
        {
            var route = new LauncherFrontendRoute(page.Page);
            locations.Add(new(page.Page.ToString(), LauncherLocalizationService.ResolveRouteLabel(route, _i18n), ""));
        }
        foreach (var group in LauncherFrontendNavigationService.GetCatalog().SidebarGroups
                     .Where(group => LauncherFrontendNavigationService.GetCatalog().TopLevelPages.Any(page => page.Page == group.Page)))
        foreach (var item in group.Items)
        {
            var route = new LauncherFrontendRoute(group.Page, item.Subpage);
            locations.Add(new(group.Page + "/" + item.Subpage, LauncherLocalizationService.ResolveRouteLabel(route, _i18n), LauncherLocalizationService.ResolveRouteLabel(new(group.Page), _i18n)));
        }
        AddSettings("Setup/SetupLaunch", SetupText.Launch);
        AddSettings("Setup/SetupGameManage", SetupText.GameManage);
        AddSettings("Setup/SetupLauncherMisc", SetupText.LauncherMisc);
        AddSettings("Setup/SetupUI", SetupText.Ui);
        foreach (var entry in _toolsComposition.Help.Entries.Where(entry => entry.ShowInSearch))
            locations.Add(new("help/" + Uri.EscapeDataString(entry.RawPath), entry.Title, entry.Summary, ExtensionSearchKind.Help, entry.Keywords));
        return locations;
        void AddSettings(string route, object section)
        {
            foreach (var property in section.GetType().GetProperties().Where(property => property.PropertyType == typeof(string)))
                if ((property.Name.EndsWith("Title", StringComparison.Ordinal) || property.Name.EndsWith("Label", StringComparison.Ordinal))
                    && property.GetValue(section) is string title && !string.IsNullOrWhiteSpace(title))
                    locations.Add(new(route, title, locations.FirstOrDefault(location => location.Route == route)?.Title ?? "", ExtensionSearchKind.Setting));
        }
    }

    internal async ValueTask NavigateExtensionAsync(string route, IReadOnlyDictionary<string, string>? arguments, CancellationToken cancellationToken,
        string? extensionPageId = null)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!IsTopLevelNavigationInteractive) throw new ExtensionOperationException("launcher.busy", T("launch.profile.activities.busy"));
        if (route == "instance")
        {
            await SelectExtensionInstanceAsync(arguments?.GetValueOrDefault("id") ?? "", cancellationToken);
            NavigateTo(new(LauncherFrontendPageKey.InstanceSetup, LauncherFrontendSubpageKey.VersionOverall), "Extension navigation");
            return;
        }
        if (route.StartsWith("help/", StringComparison.Ordinal))
        {
            var id = Uri.UnescapeDataString(route[5..]);
            var entry = _toolsComposition.Help.Entries.FirstOrDefault(entry => entry.RawPath == id)
                        ?? throw new ExtensionOperationException("location.not-found", T("extensions.unavailable"));
            OpenHelpTopic(entry);
            return;
        }
        var parts = route.Split('/');
        if (!Enum.TryParse<LauncherFrontendPageKey>(parts[0], out var page) || !Enum.IsDefined(page)) throw new ArgumentException("Unknown launcher route.", nameof(route));
        var subpage = parts.Length > 1 && Enum.TryParse<LauncherFrontendSubpageKey>(parts[1], out var parsed) && Enum.IsDefined(parsed)
            ? parsed : LauncherFrontendSubpageKey.Default;
        NavigateToTarget(new(page, subpage), "Extension navigation", extensionPageId: extensionPageId);
        if (arguments?.TryGetValue("query", out var query) == true)
        {
            if (subpage == LauncherFrontendSubpageKey.DownloadInstall) DownloadInstallMinecraftSearchQuery = query;
            else if (subpage == LauncherFrontendSubpageKey.ToolsLauncherHelp) HelpSearchQuery = query;
        }
    }

    internal async ValueTask LaunchExtensionInstanceAsync(string id, CancellationToken cancellationToken)
    {
        if (!IsTopLevelNavigationInteractive || !LaunchCommand.CanExecute(null)) throw new ExtensionOperationException("launcher.busy", T("launch.profile.activities.busy"));
        await SelectExtensionInstanceAsync(id, cancellationToken);
        NavigateTo(new(LauncherFrontendPageKey.Launch), "Extension launch");
        if (!LaunchCommand.CanExecute(null)) throw new ExtensionOperationException("launcher.busy", T("launch.profile.activities.busy"));
        LaunchCommand.Execute(null);
    }

    private async ValueTask SelectExtensionInstanceAsync(string id, CancellationToken cancellationToken)
    {
        var instance = (await GetExtensionInstancesAsync(cancellationToken)).FirstOrDefault(instance => string.Equals(instance.Id, id, GetPathComparison()))
                       ?? throw new ExtensionOperationException("instance.not-found", T("extensions.unavailable"));
        var launcher = Path.GetDirectoryName(Path.GetDirectoryName(instance.Directory))!;
        if (!string.Equals(launcher, _instanceComposition.Selection.LauncherDirectory, GetPathComparison()))
        {
            RefreshSelectedLauncherFolderSmoothly(StoreLauncherFolderPath(launcher, _launcherActionService.RuntimePaths), launcher, "Extension folder selection");
            await AwaitLatestSelectedInstanceRefreshAsync();
        }
        RefreshSelectedInstanceSmoothly(instance.Name);
        await AwaitLatestSelectedInstanceRefreshAsync();
        cancellationToken.ThrowIfCancellationRequested();
    }
}
