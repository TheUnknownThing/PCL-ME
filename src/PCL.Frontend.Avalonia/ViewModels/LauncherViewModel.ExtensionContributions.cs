using System.Collections.ObjectModel;
using PCL.Core.App.Essentials;
using PCL.Extensions.Avalonia;
using PCL.Extensions.Contracts;
using PCL.UI.Commands;
using PCL.Frontend.Avalonia.Workflows;
using PCL.Frontend.Avalonia.Models;

namespace PCL.Frontend.Avalonia.ViewModels;

internal sealed record ExtensionNavigationEntry(string Id, SidebarListItemViewModel Item);

internal sealed partial class LauncherViewModel
{
    public ObservableCollection<ExtensionNavigationEntry> ExtensionNavigationEntries { get; } = [];
    private object? _extensionPageContent;
    private string? _extensionPageId;
    private int _extensionPageRevision;
    public object? ExtensionPageContent => _extensionPageContent;
    public bool HasExtensionPage => _extensionPageContent is not null;
    internal string ExtensionRoute => _currentRoute.Page + "/" + _currentRoute.Subpage;
    internal string ExtensionLauncherDirectory => _instanceComposition.Selection.LauncherDirectory;
    internal bool CanMutateExtensionInstances => IsTopLevelNavigationInteractive && !_isLaunchInProgress && _activeLaunchProcess is null;
    internal bool CanMutateDuringExtensionHook => _activeLaunchProcess is null;
    internal void RefreshExtensionInstances() => ReloadInstanceComposition(initializeAllSurfaces: false);
    internal string? ExtensionSelectedWorldPath => _versionSavesComposition.Selection.HasSelection &&
        _versionSavesComposition.Selection.InstanceName == _instanceComposition.Selection.InstanceName ? _versionSavesComposition.Selection.SavePath : null;
    internal void RefreshExtensionResourceProviders()
    {
        if (!IsCurrentStandardRightPane(ViewModels.Panes.StandardRightPaneKind.DownloadResource)) return;
        var selected = GetSelectedFilterValue(DownloadResourceSourceOptions, SelectedDownloadResourceSourceIndex);
        if (_downloadResourceRuntimeStates.TryGetValue(_currentRoute.Subpage, out var state))
            _downloadResourceSourceOptions = BuildDownloadResourceSourceOptions(state);
        else
            _downloadResourceSourceOptions = DownloadResourceSourceOptions.Where(option => !option.FilterValue.Contains('.')).Concat(
                ExtensionSession?.GetResourceProviderDefinitions().Where(provider => !provider.Id.StartsWith("pcl.", StringComparison.Ordinal) &&
                    Desktop.Extensions.FrontendExtensionSession.ResourceKind(_currentRoute.Subpage) is { } kind && provider.Kinds.Contains(kind))
                    .Select(provider => CreateDownloadResourceFilterOption(provider.Title, provider.Id)) ?? []).ToArray();
        _selectedDownloadResourceSourceIndex = Math.Max(0, FindFilterOptionIndex(DownloadResourceSourceOptions, selected));
        RaiseDownloadResourceFilterState();
        if (selected.Length > 0 && !DownloadResourceSourceOptions.Any(option => option.FilterValue == selected))
            ScheduleDownloadResourceRefresh(immediate: true, resetPage: true);
    }
    internal ExtensionPageArea? ExtensionPageArea => _currentRoute.Page switch
    {
        LauncherFrontendPageKey.Download => PCL.Extensions.Avalonia.ExtensionPageArea.Download,
        LauncherFrontendPageKey.Setup => PCL.Extensions.Avalonia.ExtensionPageArea.Settings,
        LauncherFrontendPageKey.Tools => PCL.Extensions.Avalonia.ExtensionPageArea.Tools,
        _ => null
    };
    internal void SetExtensionPage(string? id, object? content, bool refreshPanes = true,
        NavigationTransitionDirection direction = NavigationTransitionDirection.Forward, bool requestTransition = true)
    {
        _extensionPageId = id;
        if (SetProperty(ref _extensionPageContent, content, nameof(ExtensionPageContent)))
        {
            _extensionPageRevision++;
            if (content is not null)
                foreach (var entry in SidebarSections.SelectMany(section => section.Items)) entry.IsSelected = false;
            else if (_currentNavigation is not null)
                ReplaceSidebarSectionsIfChanged(ResolveSidebarNavigation(_currentNavigation));
            RaisePropertyChanged(nameof(HasExtensionPage));
            foreach (var entry in ExtensionNavigationEntries) entry.Item.IsSelected = entry.Id == id;
            if (refreshPanes)
            {
                var previousLeftKey = CurrentStandardLeftPaneDescriptor?.Key;
                var previousRightKey = CurrentStandardRightPaneDescriptor?.Key;
                RefreshStandardPanes();
                if (content is null) RefreshActiveRightPaneSurface();
                if (requestTransition) RequestNavigationTransition(direction, IsLaunchRoute, previousLeftKey, previousRightKey);
            }
        }
    }
    internal void SetExtensionNavigation(ExtensionNavigationEntry[] entries)
    {
        for (var index = ExtensionNavigationEntries.Count - 1; index >= 0; index--)
        {
            var current = ExtensionNavigationEntries[index];
            if (!entries.Any(next => next.Id == current.Id && next.Item.Title == current.Item.Title))
                ExtensionNavigationEntries.RemoveAt(index);
        }
        for (var index = 0; index < entries.Length; index++)
        {
            var next = entries[index];
            var current = ExtensionNavigationEntries.FirstOrDefault(item => item.Id == next.Id && item.Item.Title == next.Item.Title);
            if (current is null) ExtensionNavigationEntries.Insert(index, next);
            else
            {
                current.Item.IsSelected = next.Item.IsSelected;
                var previousIndex = ExtensionNavigationEntries.IndexOf(current);
                if (previousIndex != index) ExtensionNavigationEntries.Move(previousIndex, index);
            }
        }
        while (ExtensionNavigationEntries.Count > entries.Length) ExtensionNavigationEntries.RemoveAt(ExtensionNavigationEntries.Count - 1);
    }
    internal ExtensionSurfaceContext GetExtensionSurfaceContext(bool install = false, string? region = null)
    {
        var surface = new ExtensionSurfaceContext
        {
            InstanceId = ExtensionSelectedInstanceId,
            MinecraftVersion = install && _downloadInstallMinecraftChoice is { } choice ? choice.Version : _instanceComposition.Selection.VanillaVersion,
            Loader = install ? _downloadInstallSelections.FirstOrDefault(pair => ManagedPrimaryInstallTitles.Contains(pair.Key) && pair.Value.SelectedChoice is not null).Key?.Replace(" ", "").ToLowerInvariant() : GetExtensionLoader(),
            WorldPath = ExtensionSelectedWorldPath
        };
        if (region != ExtensionRegions.DownloadResourceExtras || Desktop.Extensions.FrontendExtensionSession.ResourceKind(_currentRoute.Subpage) is not { } kind) return surface;
        var query = BuildCurrentDownloadResourceQuery();
        var provider = query.Source switch { "Modrinth" => "pcl.modrinth", "CurseForge" => "pcl.curseforge", "" => null, _ => query.Source };
        return surface with
        {
            MinecraftVersion = string.IsNullOrEmpty(query.Version) ? null : query.Version,
            Loader = string.IsNullOrEmpty(query.Loader) ? null : query.Loader,
            ResourceQuery = new() { Kind = kind, Text = query.SearchText, ProviderId = provider,
                MinecraftVersion = string.IsNullOrEmpty(query.Version) ? null : query.Version, Loader = string.IsNullOrEmpty(query.Loader) ? null : query.Loader }
        };
    }
    private FrontendInstanceComposition? _extensionLoaderComposition;
    private string? _extensionLoader;
    private string? GetExtensionLoader()
    {
        if (ReferenceEquals(_extensionLoaderComposition, _instanceComposition)) return _extensionLoader;
        _extensionLoaderComposition = _instanceComposition;
        var profile = FrontendVersionManifestInspector.ReadProfile(_instanceComposition.Selection.LauncherDirectory, _instanceComposition.Selection.InstanceName);
        return _extensionLoader = FrontendVersionManifestInspector.CreateLoaderMap(profile).Keys.FirstOrDefault();
    }
}
