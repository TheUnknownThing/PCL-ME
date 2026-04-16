using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using Avalonia.Media.Imaging;
using fNbt;
using PCL.Core.App.Essentials;
using PCL.Core.Minecraft;
using PCL.Frontend.Avalonia.Models;
using PCL.Frontend.Avalonia.ViewModels.ShellPanes;
using PCL.Frontend.Avalonia.Workflows;

namespace PCL.Frontend.Avalonia.ViewModels;

internal sealed partial class FrontendShellViewModel
{
    private enum InstanceResourceFilter
    {
        All = 0,
        Enabled = 1,
        Disabled = 2,
        Duplicate = 3
    }

    private enum InstanceWorldSortMethod
    {
        FileName = 0,
        CreateTime = 1,
        ModifyTime = 2
    }

    private enum InstanceResourceSortMethod
    {
        FileName = 0,
        ResourceName = 1,
        CreateTime = 2,
        FileSize = 3
    }

    private string _instanceWorldSearchQuery = string.Empty;
    private string _instanceServerSearchQuery = string.Empty;
    private string _instanceResourceSearchQuery = string.Empty;
    private string _instanceResourceSurfaceTitle = "Mod";
    private InstanceResourceFilter _instanceResourceFilter = InstanceResourceFilter.All;
    private int _instanceResourceTotalCount;
    private int _instanceResourceEnabledCount;
    private int _instanceResourceDisabledCount;
    private int _instanceResourceDuplicateCount;
    private bool _instanceResourceIsSearching;
    private InstanceWorldSortMethod _instanceWorldSortMethod = InstanceWorldSortMethod.FileName;
    private InstanceResourceSortMethod _instanceResourceSortMethod = InstanceResourceSortMethod.ResourceName;
    private readonly HashSet<string> _instanceResourceSelectedPaths = new(StringComparer.OrdinalIgnoreCase);
    private bool _suppressInstanceResourceSelectionChanged;
    private bool _hasInstanceResourceRefreshSnapshot;
    private LauncherFrontendSubpageKey _instanceResourceRefreshSubpage;
    private IReadOnlyList<FrontendInstanceResourceEntry>? _instanceResourceRefreshSourceEntries;
    private string _instanceResourceRefreshSearchQuery = string.Empty;
    private InstanceResourceFilter _instanceResourceRefreshFilter = InstanceResourceFilter.All;
    private InstanceResourceSortMethod _instanceResourceRefreshSortMethod = InstanceResourceSortMethod.ResourceName;

    public string InstanceWorldSearchQuery
    {
        get => _instanceWorldSearchQuery;
        set
        {
            if (SetProperty(ref _instanceWorldSearchQuery, value))
            {
                RefreshInstanceWorldEntries();
            }
        }
    }

    public string InstanceServerSearchQuery
    {
        get => _instanceServerSearchQuery;
        set
        {
            if (SetProperty(ref _instanceServerSearchQuery, value))
            {
                RefreshInstanceServerEntries();
            }
        }
    }

    public string InstanceResourceSearchQuery
    {
        get => _instanceResourceSearchQuery;
        set
        {
            if (SetProperty(ref _instanceResourceSearchQuery, value))
            {
                RefreshInstanceResourceEntries();
            }
        }
    }

    public string InstanceResourceSurfaceTitle
    {
        get => _instanceResourceSurfaceTitle;
        private set => SetProperty(ref _instanceResourceSurfaceTitle, value);
    }

    public bool HasInstanceWorldEntries => InstanceWorldEntries.Count > 0;

    public bool HasNoInstanceWorldEntries => !HasInstanceWorldEntries;

    public bool ShowInstanceWorldContent => _instanceComposition.World.Entries.Count > 0;

    public bool ShowInstanceWorldEmptyState => !ShowInstanceWorldContent;

    public string InstanceWorldSortText => SD("instance.content.sort.label", ("mode", GetInstanceWorldSortName(_instanceWorldSortMethod)));

    public bool HasInstanceScreenshotEntries => InstanceScreenshotEntries.Count > 0;

    public bool HasNoInstanceScreenshotEntries => !HasInstanceScreenshotEntries;

    public bool ShowInstanceScreenshotContent => _instanceComposition.Screenshot.Entries.Count > 0;

    public bool ShowInstanceScreenshotEmptyState => !ShowInstanceScreenshotContent;

    public bool HasInstanceServerEntries => InstanceServerEntries.Count > 0;

    public bool HasNoInstanceServerEntries => !HasInstanceServerEntries;

    public bool ShowInstanceServerContent => _instanceComposition.Server.Entries.Count > 0;

    public bool ShowInstanceServerEmptyState => !ShowInstanceServerContent;

    public bool HasInstanceResourceEntries => InstanceResourceEntries.Count > 0;

    public bool HasNoInstanceResourceEntries => !HasInstanceResourceEntries;

    public bool ShowInstanceResourceUnsupportedState => _currentRoute.Subpage switch
    {
        LauncherFrontendSubpageKey.VersionMod => !_instanceComposition.Selection.IsModable,
        LauncherFrontendSubpageKey.VersionSchematic => GetCurrentInstanceResourceSourceEntries().Count == 0,
        _ => false
    };

    public bool ShowInstanceResourceContent => !ShowInstanceResourceUnsupportedState
        && GetCurrentInstanceResourceSourceEntries().Count > 0;

    public bool ShowInstanceResourceEmptyState => ShowInstanceResourceUnsupportedState
        || GetCurrentInstanceResourceSourceEntries().Count == 0;

    public bool ShowInstanceResourceFilterBar => _currentRoute.Subpage == LauncherFrontendSubpageKey.VersionMod
        && _instanceComposition.Selection.IsModable;

    public string InstanceResourceFilterAllText => SD(
        _instanceResourceIsSearching ? "instance.content.resource.filters.search_results" : "instance.content.resource.filters.all",
        ("count", _instanceResourceTotalCount));

    public string InstanceResourceSortText => SD("instance.content.sort.label", ("mode", GetInstanceResourceSortName(_instanceResourceSortMethod)));

    public bool IsInstanceResourceFilterAllSelected => _instanceResourceFilter == InstanceResourceFilter.All;

    public string InstanceResourceFilterEnabledText => SD("instance.content.resource.filters.enabled", ("count", _instanceResourceEnabledCount));

    public bool ShowInstanceResourceEnabledFilter => ShowInstanceResourceFilterBar
        && (_instanceResourceFilter == InstanceResourceFilter.Enabled
            || (_instanceResourceEnabledCount > 0 && _instanceResourceEnabledCount < _instanceResourceTotalCount));

    public bool IsInstanceResourceFilterEnabledSelected => _instanceResourceFilter == InstanceResourceFilter.Enabled;

    public string InstanceResourceFilterDisabledText => SD("instance.content.resource.filters.disabled", ("count", _instanceResourceDisabledCount));

    public bool ShowInstanceResourceDisabledFilter => ShowInstanceResourceFilterBar
        && (_instanceResourceFilter == InstanceResourceFilter.Disabled || _instanceResourceDisabledCount > 0);

    public bool IsInstanceResourceFilterDisabledSelected => _instanceResourceFilter == InstanceResourceFilter.Disabled;

    public string InstanceResourceFilterDuplicateText => SD("instance.content.resource.filters.duplicate", ("count", _instanceResourceDuplicateCount));

    public bool ShowInstanceResourceDuplicateFilter => ShowInstanceResourceFilterBar
        && (_instanceResourceFilter == InstanceResourceFilter.Duplicate || _instanceResourceDuplicateCount > 0);

    public bool IsInstanceResourceFilterDuplicateSelected => _instanceResourceFilter == InstanceResourceFilter.Duplicate;

    public bool ShowInstanceResourceEmptyInstallActions => !ShowInstanceResourceUnsupportedState;

    public bool ShowInstanceResourceInstanceSelectAction => ShowInstanceResourceUnsupportedState;

    public string InstanceResourceSearchWatermark => _currentRoute.Subpage switch
    {
        LauncherFrontendSubpageKey.VersionResourcePack => SD("instance.content.resource.search.resource_pack"),
        LauncherFrontendSubpageKey.VersionShader => SD("instance.content.resource.search.shader"),
        LauncherFrontendSubpageKey.VersionSchematic => SD("instance.content.resource.search.schematic"),
        _ => SD("instance.content.resource.search.default")
    };

    public string InstanceResourceDownloadButtonText => _currentRoute.Subpage switch
    {
        LauncherFrontendSubpageKey.VersionResourcePack => SD("instance.content.resource.actions.download_new"),
        LauncherFrontendSubpageKey.VersionShader => SD("instance.content.resource.actions.download_new"),
        LauncherFrontendSubpageKey.VersionSchematic => SD("instance.content.resource.actions.download_schematic_mod"),
        _ => SD("instance.content.resource.actions.download_new")
    };

    public string InstanceResourceEmptyTitle => _currentRoute.Subpage switch
    {
        LauncherFrontendSubpageKey.VersionMod when !_instanceComposition.Selection.IsModable => SD("instance.content.resource.empty.mod_unavailable_title"),
        LauncherFrontendSubpageKey.VersionMod => SD("instance.content.resource.empty.default_title"),
        LauncherFrontendSubpageKey.VersionResourcePack => SD("instance.content.resource.empty.default_title"),
        LauncherFrontendSubpageKey.VersionShader => SD("instance.content.resource.empty.default_title"),
        LauncherFrontendSubpageKey.VersionSchematic => SD("instance.content.resource.empty.schematic_unavailable_title"),
        _ => SD("instance.content.resource.empty.default_title")
    };

    public string InstanceResourceEmptyDescription => _currentRoute.Subpage switch
    {
        LauncherFrontendSubpageKey.VersionMod when !_instanceComposition.Selection.IsModable =>
            SD("instance.content.resource.empty.mod_unavailable_description"),
        LauncherFrontendSubpageKey.VersionSchematic => SD("instance.content.resource.empty.schematic_unavailable_description"),
        _ => SD("instance.content.resource.empty.default_description")
    };

    public string InstanceResourceEmptyDownloadButtonText => _currentRoute.Subpage switch
    {
        LauncherFrontendSubpageKey.VersionMod when !_instanceComposition.Selection.IsModable => SD("instance.content.resource.actions.go_to_download"),
        LauncherFrontendSubpageKey.VersionSchematic => SD("instance.content.resource.actions.download_schematic_mod"),
        _ => InstanceResourceDownloadButtonText
    };

    public bool ShowInstanceResourceCheckButton => _currentRoute.Subpage == LauncherFrontendSubpageKey.VersionMod
        && _instanceComposition.Selection.IsModable
        && FrontendUiVisibilityService.ShouldShowModUpdateAction(GetUiVisibilityPreferences());

    public int InstanceResourceSelectedCount => _instanceResourceSelectedPaths.Count;

    public bool HasSelectedInstanceResources => InstanceResourceSelectedCount > 0;

    public string InstanceResourceSelectionText => SD("instance.content.resource.selection", ("count", InstanceResourceSelectedCount));

    public bool ShowInstanceResourceDefaultActions => !HasSelectedInstanceResources;

    public bool ShowInstanceResourceBatchActions => HasSelectedInstanceResources;

    public bool ShowInstanceResourceToggleActions => IsInstanceResourceToggleSupported();

    public bool CanSelectAllInstanceResources => InstanceResourceEntries.Count > 0
        && InstanceResourceSelectedCount < InstanceResourceEntries.Count;

    public bool CanEnableSelectedInstanceResources => ShowInstanceResourceToggleActions
        && InstanceResourceEntries.Any(entry => entry.IsSelected && !entry.IsEnabledState);

    public bool CanDisableSelectedInstanceResources => ShowInstanceResourceToggleActions
        && InstanceResourceEntries.Any(entry => entry.IsSelected && entry.IsEnabledState);

    public bool CanDeleteSelectedInstanceResources => HasSelectedInstanceResources;

    public ActionCommand OpenInstanceWorldFolderCommand => new(() =>
        OpenInstanceDirectoryTarget(
            SD("instance.content.world.actions.open_folder"),
            _instanceComposition.Selection.HasSelection ? Path.Combine(_instanceComposition.Selection.IndieDirectory, "saves") : string.Empty,
            SD("instance.content.world.errors.no_directory")));

    public ActionCommand PasteInstanceWorldClipboardCommand => new(() => _ = PasteInstanceWorldClipboardAsync());

    public ActionCommand OpenInstanceScreenshotFolderCommand => new(() =>
        OpenInstanceDirectoryTarget(
            SD("instance.content.screenshot.actions.open_folder"),
            _instanceComposition.Selection.HasSelection ? Path.Combine(_instanceComposition.Selection.IndieDirectory, "screenshots") : string.Empty,
            SD("instance.content.screenshot.errors.no_directory")));

    public ActionCommand RefreshInstanceServerCommand => new(() => _ = RefreshAllInstanceServersAsync());

    public ActionCommand AddInstanceServerCommand => new(() => _ = AddInstanceServerAsync());

    public ActionCommand OpenInstanceResourceFolderCommand => new(() =>
        OpenInstanceDirectoryTarget(
            SD("instance.content.resource.actions.open_folder"),
            GetCurrentInstanceResourceDirectory(),
            SD("instance.content.resource.errors.directory_missing", ("surface", InstanceResourceSurfaceTitle))));

    public ActionCommand InstallInstanceResourceFromFileCommand => new(() => _ = InstallInstanceResourceFromFileAsync());

    public ActionCommand DownloadInstanceResourceCommand => new(DownloadInstanceResource);

    public ActionCommand SelectAllInstanceResourcesCommand => new(SelectAllInstanceResources);

    public ActionCommand ClearInstanceResourceSelectionCommand => new(ClearInstanceResourceSelection);

    public ActionCommand EnableSelectedInstanceResourcesCommand => new(() => _ = SetSelectedInstanceResourcesEnabledAsync(true));

    public ActionCommand DisableSelectedInstanceResourcesCommand => new(() => _ = SetSelectedInstanceResourcesEnabledAsync(false));

    public ActionCommand DeleteSelectedInstanceResourcesCommand => new(() => _ = DeleteSelectedInstanceResourcesAsync());

    public ActionCommand ExportInstanceResourceInfoCommand => new(ExportInstanceResourceInfo);

    public ActionCommand CheckInstanceModsCommand => new(CheckInstanceMods);

    public ActionCommand SetInstanceResourceAllFilterCommand => new(() => SetInstanceResourceFilter(InstanceResourceFilter.All));

    public ActionCommand SetInstanceResourceEnabledFilterCommand => new(() => SetInstanceResourceFilter(InstanceResourceFilter.Enabled));

    public ActionCommand SetInstanceResourceDisabledFilterCommand => new(() => SetInstanceResourceFilter(InstanceResourceFilter.Disabled));

    public ActionCommand SetInstanceResourceDuplicateFilterCommand => new(() => SetInstanceResourceFilter(InstanceResourceFilter.Duplicate));

    private void InitializeInstanceContentSurfaces()
    {
        _instanceWorldSearchQuery = string.Empty;
        _instanceServerSearchQuery = string.Empty;
        _instanceResourceSearchQuery = string.Empty;
        _instanceResourceSelectedPaths.Clear();
        _instanceResourceSurfaceTitle = ResolveInstanceResourceSurfaceTitle();
        _instanceResourceFilter = InstanceResourceFilter.All;
        _instanceResourceSortMethod = InstanceResourceSortMethod.ResourceName;

        RefreshInstanceWorldEntries();
        RefreshInstanceScreenshotEntries();
        RefreshInstanceServerEntries();
        RefreshInstanceResourceEntries();
    }

    private void RefreshInstanceContentSurfaces()
    {
        if (IsCurrentStandardRightPane(StandardShellRightPaneKind.InstanceWorld))
        {
            RaisePropertyChanged(nameof(InstanceWorldSearchQuery));
            RaisePropertyChanged(nameof(InstanceWorldSortText));
            RaisePropertyChanged(nameof(HasInstanceWorldEntries));
            RaisePropertyChanged(nameof(HasNoInstanceWorldEntries));
            RaisePropertyChanged(nameof(ShowInstanceWorldContent));
            RaisePropertyChanged(nameof(ShowInstanceWorldEmptyState));
        }

        if (IsCurrentStandardRightPane(StandardShellRightPaneKind.InstanceScreenshot))
        {
            RaisePropertyChanged(nameof(HasInstanceScreenshotEntries));
            RaisePropertyChanged(nameof(HasNoInstanceScreenshotEntries));
            RaisePropertyChanged(nameof(ShowInstanceScreenshotContent));
            RaisePropertyChanged(nameof(ShowInstanceScreenshotEmptyState));
        }

        if (IsCurrentStandardRightPane(StandardShellRightPaneKind.InstanceServer))
        {
            RaisePropertyChanged(nameof(InstanceServerSearchQuery));
            RaisePropertyChanged(nameof(HasInstanceServerEntries));
            RaisePropertyChanged(nameof(HasNoInstanceServerEntries));
            RaisePropertyChanged(nameof(ShowInstanceServerContent));
            RaisePropertyChanged(nameof(ShowInstanceServerEmptyState));
        }

        if (IsCurrentStandardRightPane(StandardShellRightPaneKind.InstanceResource))
        {
            RefreshInstanceResourceEntries();
            RaisePropertyChanged(nameof(InstanceResourceSearchQuery));
            RaisePropertyChanged(nameof(InstanceResourceSurfaceTitle));
            RaisePropertyChanged(nameof(InstanceResourceSearchWatermark));
            RaisePropertyChanged(nameof(InstanceResourceDownloadButtonText));
            RaisePropertyChanged(nameof(InstanceResourceEmptyTitle));
            RaisePropertyChanged(nameof(InstanceResourceEmptyDescription));
            RaisePropertyChanged(nameof(InstanceResourceEmptyDownloadButtonText));
            RaisePropertyChanged(nameof(ShowInstanceResourceCheckButton));
            RaisePropertyChanged(nameof(HasInstanceResourceEntries));
            RaisePropertyChanged(nameof(HasNoInstanceResourceEntries));
            RaisePropertyChanged(nameof(ShowInstanceResourceUnsupportedState));
            RaisePropertyChanged(nameof(ShowInstanceResourceContent));
            RaisePropertyChanged(nameof(ShowInstanceResourceEmptyState));
            RaisePropertyChanged(nameof(ShowInstanceResourceFilterBar));
            RaisePropertyChanged(nameof(InstanceResourceFilterAllText));
            RaisePropertyChanged(nameof(IsInstanceResourceFilterAllSelected));
            RaisePropertyChanged(nameof(InstanceResourceSortText));
            RaisePropertyChanged(nameof(InstanceResourceFilterEnabledText));
            RaisePropertyChanged(nameof(ShowInstanceResourceEnabledFilter));
            RaisePropertyChanged(nameof(IsInstanceResourceFilterEnabledSelected));
            RaisePropertyChanged(nameof(InstanceResourceFilterDisabledText));
            RaisePropertyChanged(nameof(ShowInstanceResourceDisabledFilter));
            RaisePropertyChanged(nameof(IsInstanceResourceFilterDisabledSelected));
            RaisePropertyChanged(nameof(InstanceResourceFilterDuplicateText));
            RaisePropertyChanged(nameof(ShowInstanceResourceDuplicateFilter));
            RaisePropertyChanged(nameof(IsInstanceResourceFilterDuplicateSelected));
            RaisePropertyChanged(nameof(ShowInstanceResourceEmptyInstallActions));
            RaisePropertyChanged(nameof(ShowInstanceResourceInstanceSelectAction));
            RaiseInstanceResourceSelectionProperties();
        }
    }

    private InstanceScreenshotEntryViewModel CreateInstanceScreenshotEntry(string title, string info, string path, Bitmap? image)
    {
        return new InstanceScreenshotEntryViewModel(
            image,
            title,
            info,
            new ActionCommand(() => OpenInstanceTarget(
                SD("instance.content.screenshot.actions.open_file"),
                path,
                SD("instance.content.screenshot.messages.missing_file"))));
    }

    private void RefreshInstanceResourceEntries()
    {
        InstanceResourceSurfaceTitle = ResolveInstanceResourceSurfaceTitle();
        var sourceEntries = GetCurrentInstanceResourceSourceEntries();
        if (!ShouldRefreshInstanceResourceEntries(sourceEntries))
        {
            return;
        }

        var searchedEntries = sourceEntries
            .Where(entry => MatchesSearch(
                entry.Title,
                entry.Description,
                entry.Summary,
                entry.Meta,
                entry.Authors,
                entry.Version,
                entry.Loader,
                entry.Website,
                InstanceResourceSearchQuery))
            .ToArray();
        var duplicateTitles = searchedEntries
            .GroupBy(entry => entry.Title, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        _instanceResourceIsSearching = !string.IsNullOrWhiteSpace(InstanceResourceSearchQuery);
        _instanceResourceTotalCount = searchedEntries.Length;
        _instanceResourceEnabledCount = searchedEntries.Count(entry => entry.IsEnabled);
        _instanceResourceDisabledCount = searchedEntries.Count(entry => !entry.IsEnabled);
        _instanceResourceDuplicateCount = searchedEntries.Count(entry => duplicateTitles.Contains(entry.Title));

        var visibleEntries = ApplyInstanceResourceSort(ApplyInstanceResourceFilter(searchedEntries, duplicateTitles))
            .ToArray();
        _instanceResourceSelectedPaths.IntersectWith(visibleEntries.Select(entry => entry.Path));

        ReplaceItems(
            InstanceResourceEntries,
            visibleEntries.Select(CreateInstanceResourceEntry));
        CaptureInstanceResourceRefreshSnapshot(sourceEntries);

        RaisePropertyChanged(nameof(HasInstanceResourceEntries));
        RaisePropertyChanged(nameof(HasNoInstanceResourceEntries));
        RaisePropertyChanged(nameof(ShowInstanceResourceUnsupportedState));
        RaisePropertyChanged(nameof(ShowInstanceResourceContent));
        RaisePropertyChanged(nameof(ShowInstanceResourceEmptyState));
        RaisePropertyChanged(nameof(ShowInstanceResourceFilterBar));
        RaisePropertyChanged(nameof(InstanceResourceFilterAllText));
        RaisePropertyChanged(nameof(IsInstanceResourceFilterAllSelected));
        RaisePropertyChanged(nameof(InstanceResourceSortText));
        RaisePropertyChanged(nameof(InstanceResourceFilterEnabledText));
        RaisePropertyChanged(nameof(ShowInstanceResourceEnabledFilter));
        RaisePropertyChanged(nameof(IsInstanceResourceFilterEnabledSelected));
        RaisePropertyChanged(nameof(InstanceResourceFilterDisabledText));
        RaisePropertyChanged(nameof(ShowInstanceResourceDisabledFilter));
        RaisePropertyChanged(nameof(IsInstanceResourceFilterDisabledSelected));
        RaisePropertyChanged(nameof(InstanceResourceFilterDuplicateText));
        RaisePropertyChanged(nameof(ShowInstanceResourceDuplicateFilter));
        RaisePropertyChanged(nameof(IsInstanceResourceFilterDuplicateSelected));
        RaisePropertyChanged(nameof(ShowInstanceResourceEmptyInstallActions));
        RaisePropertyChanged(nameof(ShowInstanceResourceInstanceSelectAction));
        RaisePropertyChanged(nameof(ShowInstanceResourceCheckButton));
        RaisePropertyChanged(nameof(InstanceResourceEmptyTitle));
        RaisePropertyChanged(nameof(InstanceResourceEmptyDescription));
        RaisePropertyChanged(nameof(InstanceResourceEmptyDownloadButtonText));
        RaiseInstanceResourceSelectionProperties();
    }

    private bool ShouldRefreshInstanceResourceEntries(IReadOnlyList<FrontendInstanceResourceEntry> sourceEntries)
    {
        if (!_hasInstanceResourceRefreshSnapshot)
        {
            return true;
        }

        return _instanceResourceRefreshSubpage != _currentRoute.Subpage
               || !ReferenceEquals(_instanceResourceRefreshSourceEntries, sourceEntries)
               || !string.Equals(_instanceResourceRefreshSearchQuery, InstanceResourceSearchQuery, StringComparison.Ordinal)
               || _instanceResourceRefreshFilter != _instanceResourceFilter
               || _instanceResourceRefreshSortMethod != _instanceResourceSortMethod;
    }

    private void CaptureInstanceResourceRefreshSnapshot(IReadOnlyList<FrontendInstanceResourceEntry> sourceEntries)
    {
        _hasInstanceResourceRefreshSnapshot = true;
        _instanceResourceRefreshSubpage = _currentRoute.Subpage;
        _instanceResourceRefreshSourceEntries = sourceEntries;
        _instanceResourceRefreshSearchQuery = InstanceResourceSearchQuery;
        _instanceResourceRefreshFilter = _instanceResourceFilter;
        _instanceResourceRefreshSortMethod = _instanceResourceSortMethod;
    }

    private IEnumerable<FrontendInstanceResourceEntry> ApplyInstanceResourceFilter(
        IReadOnlyList<FrontendInstanceResourceEntry> entries,
        ISet<string> duplicateTitles)
    {
        if (!ShowInstanceResourceFilterBar)
        {
            return entries;
        }

        return _instanceResourceFilter switch
        {
            InstanceResourceFilter.Enabled => entries.Where(entry => entry.IsEnabled),
            InstanceResourceFilter.Disabled => entries.Where(entry => !entry.IsEnabled),
            InstanceResourceFilter.Duplicate => entries.Where(entry => duplicateTitles.Contains(entry.Title)),
            _ => entries
        };
    }

    private IEnumerable<FrontendInstanceResourceEntry> ApplyInstanceResourceSort(IEnumerable<FrontendInstanceResourceEntry> entries)
    {
        return _instanceResourceSortMethod switch
        {
            InstanceResourceSortMethod.FileName => entries
                .OrderBy(entry => IsDirectoryPath(entry.Path) ? 0 : 1)
                .ThenBy(entry => Path.GetFileName(entry.Path), StringComparer.OrdinalIgnoreCase),
            InstanceResourceSortMethod.CreateTime => entries
                .OrderBy(entry => IsDirectoryPath(entry.Path) ? 0 : 1)
                .ThenByDescending(entry => GetPathCreationTimeUtc(entry.Path))
                .ThenBy(entry => entry.Title, StringComparer.OrdinalIgnoreCase),
            InstanceResourceSortMethod.FileSize => entries
                .OrderBy(entry => IsDirectoryPath(entry.Path) ? 0 : 1)
                .ThenByDescending(entry => IsDirectoryPath(entry.Path) ? 0L : GetFileSize(entry.Path))
                .ThenBy(entry => entry.Title, StringComparer.OrdinalIgnoreCase),
            _ => entries
                .OrderBy(entry => IsDirectoryPath(entry.Path) ? 0 : 1)
                .ThenBy(entry => entry.Title, StringComparer.OrdinalIgnoreCase)
        };
    }

    private IReadOnlyList<FrontendInstanceResourceEntry> GetCurrentInstanceResourceSourceEntries()
    {
        return _currentRoute.Subpage switch
        {
            LauncherFrontendSubpageKey.VersionMod => _instanceComposition.Mods.Entries.Concat(_instanceComposition.DisabledMods.Entries).ToArray(),
            LauncherFrontendSubpageKey.VersionModDisabled => _instanceComposition.DisabledMods.Entries,
            LauncherFrontendSubpageKey.VersionResourcePack => _instanceComposition.ResourcePacks.Entries,
            LauncherFrontendSubpageKey.VersionShader => _instanceComposition.Shaders.Entries,
            LauncherFrontendSubpageKey.VersionSchematic => _instanceComposition.Schematics.Entries,
            _ => _instanceComposition.Mods.Entries
        };
    }

    private void SetInstanceResourceFilter(InstanceResourceFilter filter)
    {
        if (_instanceResourceFilter == filter)
        {
            return;
        }

        _instanceResourceFilter = filter;
        RefreshInstanceResourceEntries();
    }

    internal void SetInstanceResourceFileNameSort() => SetInstanceResourceSortMethod(InstanceResourceSortMethod.FileName);

    internal void SetInstanceResourceNameSort() => SetInstanceResourceSortMethod(InstanceResourceSortMethod.ResourceName);

    internal void SetInstanceResourceCreateTimeSort() => SetInstanceResourceSortMethod(InstanceResourceSortMethod.CreateTime);

    internal void SetInstanceResourceFileSizeSort() => SetInstanceResourceSortMethod(InstanceResourceSortMethod.FileSize);

    private void SetInstanceResourceSortMethod(InstanceResourceSortMethod target)
    {
        if (_instanceResourceSortMethod == target)
        {
            return;
        }

        _instanceResourceSortMethod = target;
        RaisePropertyChanged(nameof(InstanceResourceSortText));
        RefreshInstanceResourceEntries();
    }

    private void RaiseInstanceResourceSelectionProperties()
    {
        RaisePropertyChanged(nameof(InstanceResourceSelectedCount));
        RaisePropertyChanged(nameof(HasSelectedInstanceResources));
        RaisePropertyChanged(nameof(InstanceResourceSelectionText));
        RaisePropertyChanged(nameof(ShowInstanceResourceDefaultActions));
        RaisePropertyChanged(nameof(ShowInstanceResourceBatchActions));
        RaisePropertyChanged(nameof(ShowInstanceResourceToggleActions));
        RaisePropertyChanged(nameof(CanSelectAllInstanceResources));
        RaisePropertyChanged(nameof(CanEnableSelectedInstanceResources));
        RaisePropertyChanged(nameof(CanDisableSelectedInstanceResources));
        RaisePropertyChanged(nameof(CanDeleteSelectedInstanceResources));
    }

    private bool IsInstanceResourceToggleSupported()
    {
        return _instanceComposition.Selection.IsModable
            && (_currentRoute.Subpage == LauncherFrontendSubpageKey.VersionMod
                || _currentRoute.Subpage == LauncherFrontendSubpageKey.VersionModDisabled);
    }

    private IReadOnlyList<InstanceResourceEntryViewModel> GetSelectedInstanceResourceEntries()
    {
        return InstanceResourceEntries
            .Where(entry => entry.IsSelected)
            .ToArray();
    }

    private void HandleInstanceResourceSelectionChanged(string path, bool isSelected)
    {
        if (_suppressInstanceResourceSelectionChanged)
        {
            return;
        }

        if (isSelected)
        {
            _instanceResourceSelectedPaths.Add(path);
        }
        else
        {
            _instanceResourceSelectedPaths.Remove(path);
        }

        RaiseInstanceResourceSelectionProperties();
    }

    private void SelectAllInstanceResources()
    {
        var activityTitle = SD("instance.content.resource.actions.select_all");
        if (InstanceResourceEntries.Count == 0)
        {
            AddActivity(activityTitle, SD("instance.content.resource.messages.list_empty", ("surface_title", InstanceResourceSurfaceTitle)));
            return;
        }

        if (!CanSelectAllInstanceResources)
        {
            return;
        }

        _suppressInstanceResourceSelectionChanged = true;
        try
        {
            foreach (var entry in InstanceResourceEntries)
            {
                entry.IsSelected = true;
            }
        }
        finally
        {
            _suppressInstanceResourceSelectionChanged = false;
        }

        _instanceResourceSelectedPaths.Clear();
        foreach (var entry in InstanceResourceEntries)
        {
            _instanceResourceSelectedPaths.Add(entry.Path);
        }

        RaiseInstanceResourceSelectionProperties();
        AddActivity(
            activityTitle,
            SD(
                "instance.content.resource.messages.selected_all",
                ("surface_title", InstanceResourceSurfaceTitle),
                ("count", InstanceResourceEntries.Count)));
    }

    private void ClearInstanceResourceSelection()
    {
        if (!HasSelectedInstanceResources)
        {
            return;
        }

        _suppressInstanceResourceSelectionChanged = true;
        try
        {
            foreach (var entry in InstanceResourceEntries)
            {
                entry.IsSelected = false;
            }
        }
        finally
        {
            _suppressInstanceResourceSelectionChanged = false;
        }

        _instanceResourceSelectedPaths.Clear();
        RaiseInstanceResourceSelectionProperties();
    }

    private InstanceResourceEntryViewModel CreateInstanceResourceEntry(FrontendInstanceResourceEntry entry)
    {
        var display = IsInstanceResourceToggleSupported()
            ? FrontendGameManagementService.ResolveLocalModDisplay(entry, SelectedModLocalNameStyleIndex)
            : new FrontendLocalModDisplay(entry.Title, entry.Summary);
        var detailCommand = new ActionCommand(() => _ = ShowInstanceResourceDetailsAsync(entry));
        var websiteCommand = string.IsNullOrWhiteSpace(entry.Website)
            ? null
            : CreateOpenTargetCommand(
                SD(
                    "instance.content.resource.activities.open_homepage",
                    ("surface_title", InstanceResourceSurfaceTitle),
                    ("entry_title", entry.Title)),
                entry.Website,
                entry.Website);
        var openCommand = new ActionCommand(() =>
            OpenInstanceTarget(
                SD("instance.content.resource.tooltips.open_file_location"),
                entry.Path,
                SD("instance.content.resource.messages.entry_missing", ("surface_title", InstanceResourceSurfaceTitle))));
        var toggleCommand = IsInstanceResourceToggleSupported()
            ? new ActionCommand(() => _ = SetInstanceResourceEntriesEnabledAsync(
                new[] { (Title: entry.Title, Path: entry.Path, IsEnabledState: entry.IsEnabled) },
                !entry.IsEnabled,
                SD("instance.content.resource.messages.no_toggleable_entries")))
            : null;
        var deleteCommand = new ActionCommand(() => _ = DeleteInstanceResourcesAsync(
            new[] { (Title: entry.Title, Path: entry.Path) },
            SD("instance.content.resource.messages.no_deletable_entries")));

        return new InstanceResourceEntryViewModel(
            LoadInstanceResourceBitmap(entry),
            display.Title,
            LocalizeResourceSummary(display.Summary),
            LocalizeResourceMeta(entry.Meta),
            entry.Path,
            openCommand,
            actionToolTip: SD("instance.content.resource.tooltips.open_file_location"),
            isEnabled: entry.IsEnabled,
            description: entry.Description,
            website: entry.Website,
            showSelection: true,
            isSelected: _instanceResourceSelectedPaths.Contains(entry.Path),
            selectionChanged: isSelected => HandleInstanceResourceSelectionChanged(entry.Path, isSelected),
            infoCommand: detailCommand,
            websiteCommand: websiteCommand,
            openCommand: openCommand,
            toggleCommand: toggleCommand,
            deleteCommand: deleteCommand,
            infoToolTip: SD("instance.content.resource.tooltips.details"),
            websiteToolTip: SD("instance.content.resource.tooltips.website"),
            openToolTip: SD("instance.content.resource.tooltips.open_file_location"),
            enableToolTip: SD("instance.content.resource.tooltips.enable"),
            disableToolTip: SD("instance.content.resource.tooltips.disable"),
            deleteToolTip: SD("instance.content.resource.tooltips.delete"),
            disabledTagText: SD("instance.content.resource.tags.disabled"));
    }

    private async Task ShowInstanceResourceDetailsAsync(FrontendInstanceResourceEntry entry)
    {
        var lines = new List<string>
        {
            $"{SD("instance.content.resource.details.fields.name")}: {entry.Title}",
            $"{SD("instance.content.resource.details.fields.status")}: {(entry.IsEnabled ? SD("instance.content.resource.tooltips.enable") : SD("instance.content.resource.tooltips.disable"))}"
        };

        if (!string.IsNullOrWhiteSpace(entry.Meta))
        {
            lines.Add($"{SD("instance.content.resource.details.fields.type")}: {entry.Meta}");
        }

        if (!string.IsNullOrWhiteSpace(entry.Loader))
        {
            lines.Add($"{SD("instance.content.resource.details.fields.loader")}: {entry.Loader}");
        }

        if (!string.IsNullOrWhiteSpace(entry.Version))
        {
            lines.Add($"{SD("instance.content.resource.details.fields.version")}: {entry.Version}");
        }

        if (!string.IsNullOrWhiteSpace(entry.Authors))
        {
            lines.Add($"{SD("instance.content.resource.details.fields.authors")}: {entry.Authors}");
        }

        if (!string.IsNullOrWhiteSpace(entry.Description))
        {
            lines.Add($"{SD("instance.content.resource.details.fields.description")}: {entry.Description}");
        }

        if (!string.IsNullOrWhiteSpace(entry.Website))
        {
            lines.Add($"{SD("instance.content.resource.details.fields.website")}: {entry.Website}");
        }

        if (!string.IsNullOrWhiteSpace(entry.Summary))
        {
            lines.Add($"{SD("instance.content.resource.details.fields.summary")}: {entry.Summary}");
        }

        lines.Add($"{SD("instance.content.resource.details.fields.path")}: {entry.Path}");

        try
        {
            if (Directory.Exists(entry.Path))
            {
                var directoryInfo = new DirectoryInfo(entry.Path);
                lines.Add($"{SD("instance.content.resource.details.fields.item_kind")}: {SD("instance.content.resource.meta.folder")}");
                lines.Add($"{SD("instance.content.resource.details.fields.create_time")}: {directoryInfo.CreationTime:yyyy/MM/dd HH:mm:ss}");
                lines.Add($"{SD("instance.content.resource.details.fields.modify_time")}: {directoryInfo.LastWriteTime:yyyy/MM/dd HH:mm:ss}");
            }
            else if (File.Exists(entry.Path))
            {
                var fileInfo = new FileInfo(entry.Path);
                lines.Add($"{SD("instance.content.resource.details.fields.file_size")}: {FormatInstanceResourceFileSize(fileInfo.Length)}");
                lines.Add($"{SD("instance.content.resource.details.fields.create_time")}: {fileInfo.CreationTime:yyyy/MM/dd HH:mm:ss}");
                lines.Add($"{SD("instance.content.resource.details.fields.modify_time")}: {fileInfo.LastWriteTime:yyyy/MM/dd HH:mm:ss}");
            }
        }
        catch (Exception ex)
        {
            lines.Add(SD("instance.content.resource.messages.details_read_failed", ("error", ex.Message)));
        }

        var result = await ShowToolboxConfirmationAsync(
            SD("instance.content.resource.activities.view_details", ("surface_title", InstanceResourceSurfaceTitle)),
            string.Join(Environment.NewLine, lines),
            T("common.actions.close"));
        if (result is not null)
        {
            AddActivity(SD("instance.content.resource.activities.view_details", ("surface_title", InstanceResourceSurfaceTitle)), entry.Title);
        }
    }

    private Bitmap? LoadInstanceResourceBitmap(FrontendInstanceResourceEntry entry)
    {
        if (entry.IconBytes is not null && entry.IconBytes.Length > 0)
        {
            try
            {
                using var stream = new MemoryStream(entry.IconBytes, writable: false);
                return new Bitmap(stream);
            }
            catch
            {
            }
        }

        return LoadLauncherBitmap("Images", "Blocks", entry.IconName);
    }

    private static string FormatInstanceResourceFileSize(long bytes)
    {
        string[] units = new[] { "B", "KB", "MB", "GB", "TB" };
        double size = bytes;
        var unitIndex = 0;
        while (size >= 1024 && unitIndex < units.Length - 1)
        {
            size /= 1024;
            unitIndex++;
        }

        return unitIndex == 0 ? $"{size:0} {units[unitIndex]}" : $"{size:0.##} {units[unitIndex]}";
    }

    private void RefreshInstanceWorldEntries()
    {
        var filteredEntries = _instanceComposition.World.Entries
            .Where(entry => MatchesSearch(entry.Title, entry.Summary, entry.Path, InstanceWorldSearchQuery));

        ReplaceItems(
            InstanceWorldEntries,
            ApplyInstanceWorldSort(filteredEntries)
                .Select(entry => new SimpleListEntryViewModel(
                    entry.Title,
                    entry.Summary,
                    new ActionCommand(() => OpenVersionSaveDetails(entry.Path)))));
    }

    private IEnumerable<FrontendInstanceDirectoryEntry> ApplyInstanceWorldSort(IEnumerable<FrontendInstanceDirectoryEntry> entries)
    {
        return _instanceWorldSortMethod switch
        {
            InstanceWorldSortMethod.CreateTime => entries
                .OrderByDescending(entry => GetDirectoryCreationTimeUtc(entry.Path))
                .ThenBy(entry => entry.Title, StringComparer.OrdinalIgnoreCase),
            InstanceWorldSortMethod.ModifyTime => entries
                .OrderByDescending(entry => GetDirectoryLastWriteTimeUtc(entry.Path))
                .ThenBy(entry => entry.Title, StringComparer.OrdinalIgnoreCase),
            _ => entries.OrderBy(entry => entry.Title, StringComparer.OrdinalIgnoreCase)
        };
    }

    private void SetInstanceWorldSortMethod(InstanceWorldSortMethod target)
    {
        if (_instanceWorldSortMethod == target)
        {
            return;
        }

        _instanceWorldSortMethod = target;

        RaisePropertyChanged(nameof(InstanceWorldSortText));
        RefreshInstanceWorldEntries();
    }

    internal void SetInstanceWorldFileNameSort() => SetInstanceWorldSortMethod(InstanceWorldSortMethod.FileName);

    internal void SetInstanceWorldCreateTimeSort() => SetInstanceWorldSortMethod(InstanceWorldSortMethod.CreateTime);

    internal void SetInstanceWorldModifyTimeSort() => SetInstanceWorldSortMethod(InstanceWorldSortMethod.ModifyTime);

    private string GetInstanceWorldSortName(InstanceWorldSortMethod method)
    {
        return method switch
        {
            InstanceWorldSortMethod.CreateTime => SD("instance.content.sort.create_time"),
            InstanceWorldSortMethod.ModifyTime => SD("instance.content.sort.modify_time"),
            _ => SD("instance.content.sort.file_name")
        };
    }

    private string GetInstanceResourceSortName(InstanceResourceSortMethod method)
    {
        return method switch
        {
            InstanceResourceSortMethod.FileName => SD("instance.content.sort.file_name"),
            InstanceResourceSortMethod.CreateTime => SD("instance.content.sort.added_time"),
            InstanceResourceSortMethod.FileSize => SD("instance.content.sort.file_size"),
            _ => SD("instance.content.sort.resource_name")
        };
    }

    private static DateTime GetDirectoryCreationTimeUtc(string path)
    {
        try
        {
            return Directory.GetCreationTimeUtc(path);
        }
        catch
        {
            return DateTime.MinValue;
        }
    }

    private static DateTime GetDirectoryLastWriteTimeUtc(string path)
    {
        try
        {
            return Directory.GetLastWriteTimeUtc(path);
        }
        catch
        {
            return DateTime.MinValue;
        }
    }

    private static DateTime GetPathCreationTimeUtc(string path)
    {
        try
        {
            return IsDirectoryPath(path) ? Directory.GetCreationTimeUtc(path) : File.GetCreationTimeUtc(path);
        }
        catch
        {
            return DateTime.MinValue;
        }
    }

    private static long GetFileSize(string path)
    {
        try
        {
            return File.Exists(path) ? new FileInfo(path).Length : 0L;
        }
        catch
        {
            return 0L;
        }
    }

    private static bool IsDirectoryPath(string path)
    {
        try
        {
            return Directory.Exists(path);
        }
        catch
        {
            return false;
        }
    }

    private void RefreshInstanceScreenshotEntries()
    {
        ReplaceItems(
            InstanceScreenshotEntries,
            _instanceComposition.Screenshot.Entries.Select(entry => CreateInstanceScreenshotEntry(
                entry.Title,
                LocalizeResourceSummary(entry.Summary),
                entry.Path,
                LoadInstanceBitmap(entry.Path, "Images", "Backgrounds", "server_bg.png"))));
    }

    private void RefreshInstanceServerEntries()
    {
        ReplaceItems(
            InstanceServerEntries,
            _instanceComposition.Server.Entries
                .Where(entry => MatchesSearch(entry.Title, entry.Address, entry.Status, InstanceServerSearchQuery))
                .Select(CreateInstanceServerEntry));
    }

    private InstanceServerEntryViewModel CreateInstanceServerEntry(FrontendInstanceServerEntry entry)
    {
        InstanceServerEntryViewModel? viewModel = null;
        viewModel = new InstanceServerEntryViewModel(
            entry.Title,
            entry.Address,
            LoadLauncherBitmap("Images", "Backgrounds", "server_bg.png"),
            LoadLauncherBitmap("Images", "Icons", "DefaultServer.png"),
            new ActionCommand(() =>
            {
                if (viewModel is not null)
                {
                    _ = RefreshInstanceServerAsync(viewModel);
                }
            }),
            new ActionCommand(() =>
            {
                if (viewModel is not null)
                {
                    _ = CopyInstanceServerAddressAsync(viewModel);
                }
            }),
            new ActionCommand(() =>
            {
                if (viewModel is not null)
                {
                    _ = ConnectInstanceServerAsync(viewModel);
                }
            }),
            new ActionCommand(() => ViewInstanceServer(entry)));
        ApplyInstanceServerIdleState(viewModel, entry.Status);
        return viewModel;
    }

    private async Task RefreshAllInstanceServersAsync()
    {
        var activityTitle = SD("instance.content.server.actions.refresh_all");
        if (!_instanceComposition.Selection.HasSelection)
        {
            AddActivity(activityTitle, SD("instance.content.server.messages.no_instance_selected"));
            return;
        }

        ReloadInstanceComposition();
        var entries = InstanceServerEntries.ToArray();
        if (entries.Length == 0)
        {
            AddActivity(activityTitle, SD("instance.content.server.messages.no_saved_servers"));
            return;
        }

        await Task.WhenAll(entries.Select(entry => RefreshInstanceServerAsync(entry, addActivity: false)));
        AddActivity(activityTitle, SD("instance.content.server.messages.refreshed_count", ("count", entries.Length)));
    }

    private async Task RefreshInstanceServerAsync(InstanceServerEntryViewModel entry, bool addActivity = true)
    {
        var activityTitle = SD("instance.content.server.actions.refresh");
        var address = (entry.Address ?? string.Empty).Trim().Replace("：", ":");
        if (string.IsNullOrWhiteSpace(address))
        {
            ApplyInstanceServerErrorState(entry, SD("instance.content.server.messages.address_empty"));
            if (addActivity)
            {
                AddFailureActivity(T("common.activities.failed", ("title", activityTitle)), BuildActivityDetail(entry.Title, SD("instance.content.server.messages.address_empty")));
            }

            return;
        }

        ApplyInstanceServerLoadingState(entry);

        try
        {
            var reachableAddress = await ResolveMinecraftServerQueryEndpointAsync(address, CancellationToken.None);
            using var queryService = global::PCL.Core.Link.McPing.McPingServiceFactory.CreateService(reachableAddress.Ip, reachableAddress.Port);
            var result = await queryService.PingAsync(CancellationToken.None);
            if (result is null)
            {
                throw new InvalidOperationException(SD("instance.content.server.messages.no_server_info"));
            }

            ApplyInstanceServerSuccessState(entry, result);
            if (addActivity)
            {
                AddActivity(activityTitle, BuildActivityDetail(entry.Title, $"{result.Players.Online}/{result.Players.Max}", $"{result.Latency}ms"));
            }
        }
        catch (Exception ex)
        {
            ApplyInstanceServerErrorState(entry, ex.Message);
            if (addActivity)
            {
                AddFailureActivity(T("common.activities.failed", ("title", activityTitle)), BuildActivityDetail(entry.Title, ex.Message));
            }
        }
    }

    private void ApplyInstanceServerIdleState(InstanceServerEntryViewModel entry, string status)
    {
        entry.StatusText = string.IsNullOrWhiteSpace(status) ? SD("instance.content.server.status.saved") : LocalizeServerStatusText(status);
        entry.StatusBrush = global::Avalonia.Media.Brushes.White;
        entry.PlayerCount = "-/-";
        entry.Latency = string.Empty;
        entry.LatencyBrush = global::Avalonia.Media.Brushes.White;
        entry.PlayerTooltip = null;
        entry.MotdLines = BuildMinecraftServerQueryMotdLines(SD("instance.content.server.motd.click_to_refresh"));
    }

    private void ApplyInstanceServerLoadingState(InstanceServerEntryViewModel entry)
    {
        entry.StatusText = SD("instance.content.server.status.connecting");
        entry.StatusBrush = global::Avalonia.Media.Brushes.White;
        entry.PlayerCount = SD("instance.content.server.status.connecting_short");
        entry.Latency = string.Empty;
        entry.LatencyBrush = global::Avalonia.Media.Brushes.White;
        entry.PlayerTooltip = null;
        entry.MotdLines = BuildMinecraftServerQueryMotdLines(SD("instance.content.server.motd.connecting"));
        entry.Logo = LoadLauncherBitmap("Images", "Icons", "DefaultServer.png");
    }

    private void ApplyInstanceServerSuccessState(InstanceServerEntryViewModel entry, global::PCL.Core.Link.McPing.Model.McPingResult result)
    {
        entry.StatusText = SD("instance.content.server.status.online");
        entry.StatusBrush = global::Avalonia.Media.Brushes.White;
        entry.PlayerCount = $"{result.Players.Online}/{result.Players.Max}";
        entry.Latency = $"{result.Latency}ms";
        entry.LatencyBrush = GetMinecraftServerQueryLatencyBrush(result.Latency);
        entry.PlayerTooltip = result.Players.Samples?.Any() == true
            ? string.Join(Environment.NewLine, result.Players.Samples.Select(sample => sample.Name))
            : null;
        entry.MotdLines = BuildMinecraftServerQueryMotdLines(result.Description);
        entry.Logo = DecodeMinecraftServerQueryLogo(result.Favicon)
            ?? LoadLauncherBitmap("Images", "Icons", "DefaultServer.png");
    }

    private void ApplyInstanceServerErrorState(InstanceServerEntryViewModel entry, string message)
    {
        entry.StatusText = SD("instance.content.server.status.connection_failed", ("message", message));
        entry.StatusBrush = global::Avalonia.Media.Brushes.Red;
        entry.PlayerCount = SD("instance.content.server.status.offline_short");
        entry.Latency = string.Empty;
        entry.LatencyBrush = global::Avalonia.Media.Brushes.White;
        entry.PlayerTooltip = null;
        entry.MotdLines = BuildMinecraftServerQueryMotdLines(SD("instance.content.server.motd.offline"));
        entry.Logo = LoadLauncherBitmap("Images", "Icons", "DefaultServer.png");
    }

    private async Task CopyInstanceServerAddressAsync(InstanceServerEntryViewModel entry)
    {
        var activityTitle = SD("instance.content.server.actions.copy_address");
        var address = (entry.Address ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(address))
        {
            AddActivity(activityTitle, SD("instance.content.server.messages.no_copyable_address", ("entry_title", entry.Title)));
            return;
        }

        try
        {
            await _shellActionService.SetClipboardTextAsync(address);
            AddActivity(activityTitle, SD("instance.content.server.messages.copied_address", ("entry_title", entry.Title), ("address", address)));
        }
        catch (Exception ex)
        {
            AddFailureActivity(T("common.activities.failed", ("title", activityTitle)), ex.Message);
        }
    }

    private async Task ConnectInstanceServerAsync(InstanceServerEntryViewModel entry)
    {
        var activityTitle = SD("instance.content.server.actions.connect");
        if (!_instanceComposition.Selection.HasSelection)
        {
            AddActivity(activityTitle, SD("instance.content.server.messages.no_instance_selected"));
            return;
        }

        var address = (entry.Address ?? string.Empty).Trim().Replace("：", ":");
        if (string.IsNullOrWhiteSpace(address))
        {
            AddActivity(activityTitle, SD("instance.content.server.messages.no_connectable_address", ("entry_title", entry.Title)));
            return;
        }

        try
        {
            InstanceServerAutoJoin = address;
            RefreshLaunchState();
            NavigateTo(
                new LauncherFrontendRoute(LauncherFrontendPageKey.Launch),
                SD("instance.content.server.messages.launch_route_prepared", ("entry_title", entry.Title)));
            await HandleLaunchRequestedAsync();
            AddActivity(activityTitle, BuildActivityDetail(entry.Title, address));
        }
        catch (Exception ex)
        {
            AddFailureActivity(T("common.activities.failed", ("title", activityTitle)), ex.Message);
        }
    }

    private async Task PasteInstanceWorldClipboardAsync()
    {
        var activityTitle = SD("instance.content.world.actions.paste_clipboard");
        if (!_instanceComposition.Selection.HasSelection)
        {
            AddActivity(activityTitle, SD("instance.content.world.messages.no_instance_selected"));
            return;
        }

        string? clipboardText;
        try
        {
            clipboardText = await _shellActionService.ReadClipboardTextAsync();
        }
        catch (Exception ex)
        {
            AddFailureActivity(T("common.activities.failed", ("title", activityTitle)), ex.Message);
            return;
        }

        var sourcePaths = ParseClipboardPaths(clipboardText)
            .Where(path => File.Exists(path) || Directory.Exists(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (sourcePaths.Length == 0)
        {
            AddActivity(activityTitle, SD("instance.content.world.messages.no_importable_paths"));
            return;
        }

        var savesDirectory = Path.Combine(_instanceComposition.Selection.IndieDirectory, "saves");
        Directory.CreateDirectory(savesDirectory);

        var importedTargets = new List<string>();
        foreach (var sourcePath in sourcePaths)
        {
            var targetPath = GetUniqueChildPath(savesDirectory, Path.GetFileName(sourcePath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)));
            if (Directory.Exists(sourcePath))
            {
                CopyDirectory(sourcePath, targetPath);
            }
            else
            {
                Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
                File.Copy(sourcePath, targetPath, overwrite: false);
            }

            importedTargets.Add(targetPath);
        }

        ReloadInstanceComposition();
        AddActivity(activityTitle, string.Join(Environment.NewLine, importedTargets));
    }

    private async Task AddInstanceServerAsync()
    {
        var activityTitle = SD("instance.content.server.actions.add");
        if (!_instanceComposition.Selection.HasSelection)
        {
            AddActivity(activityTitle, SD("instance.content.server.messages.no_instance_selected"));
            return;
        }

        var serverInfo = default((bool Success, string Name, string Address, string? Activity));
        try
        {
            serverInfo = await PromptForNewInstanceServerAsync();
        }
        catch (Exception ex)
        {
            AddFailureActivity(T("common.activities.failed", ("title", activityTitle)), ex.Message);
            return;
        }

        if (!serverInfo.Success)
        {
            if (!string.IsNullOrWhiteSpace(serverInfo.Activity))
            {
                AddActivity(activityTitle, serverInfo.Activity);
            }
            return;
        }

        var name = serverInfo.Name;
        var address = serverInfo.Address;

        var serversPath = Path.Combine(_instanceComposition.Selection.IndieDirectory, "servers.dat");
        NbtList serverList;
        try
        {
            serverList = File.Exists(serversPath)
                ? LoadInstanceServerList(serversPath) ?? new NbtList("servers", NbtTagType.Compound)
                : new NbtList("servers", NbtTagType.Compound);
        }
        catch (Exception ex)
        {
            AddFailureActivity(
                T("common.activities.failed", ("title", activityTitle)),
                SD("instance.content.server.messages.read_list_failed", ("error", ex.Message)));
            return;
        }

        try
        {
            if (serverList.ListType == NbtTagType.Unknown)
            {
                serverList.ListType = NbtTagType.Compound;
            }

            serverList.Add(new NbtCompound
            {
                new NbtString("name", name),
                new NbtString("ip", address)
            });

        }
        catch (Exception ex)
        {
            AddFailureActivity(
                T("common.activities.failed", ("title", activityTitle)),
                SD("instance.content.server.messages.update_list_failed", ("error", ex.Message)));
            return;
        }

        try
        {
            var clonedServerList = (NbtList)serverList.Clone();
            if (!TryWriteInstanceServerList(serversPath, clonedServerList))
            {
                AddFailureActivity(T("common.activities.failed", ("title", activityTitle)), SD("instance.content.server.messages.write_list_failed"));
                return;
            }
        }
        catch
        {
            AddFailureActivity(T("common.activities.failed", ("title", activityTitle)), SD("instance.content.server.messages.write_list_failed"));
            return;
        }

        ReloadInstanceComposition();
        AddActivity(activityTitle, SD("instance.content.server.messages.added", ("name", name), ("address", address)));
    }

    private async Task<(bool Success, string Name, string Address, string? Activity)> PromptForNewInstanceServerAsync()
    {
        var resolvedName = await _shellActionService.PromptForTextAsync(
            SD("instance.content.server.dialogs.edit.title"),
            SD("instance.content.server.dialogs.edit.name_prompt"),
            SD("instance.content.server.dialogs.edit.name_default"));
        if (resolvedName is null)
        {
            return (false, string.Empty, string.Empty, null);
        }

        var resolvedAddress = await _shellActionService.PromptForTextAsync(
            SD("instance.content.server.dialogs.edit.title"),
            SD("instance.content.server.dialogs.edit.address_prompt"));
        if (string.IsNullOrWhiteSpace(resolvedAddress))
        {
            return resolvedAddress is null
                ? (false, string.Empty, string.Empty, null)
                : (false, string.Empty, string.Empty, SD("instance.content.server.messages.address_required"));
        }

        return (
            true,
            string.IsNullOrWhiteSpace(resolvedName) ? SD("instance.content.server.dialogs.edit.name_default") : resolvedName.Trim(),
            resolvedAddress.Trim(),
            null);
    }

    private static NbtList? LoadInstanceServerList(string serversPath)
    {
        var file = new NbtFile();
        using var stream = File.OpenRead(serversPath);
        file.LoadFromStream(stream, NbtCompression.AutoDetect);
        return file.RootTag.Get<NbtList>("servers");
    }

    private static bool TryWriteInstanceServerList(string serversPath, NbtList serverList)
    {
        var directoryPath = Path.GetDirectoryName(serversPath);
        if (string.IsNullOrWhiteSpace(directoryPath))
        {
            return false;
        }

        Directory.CreateDirectory(directoryPath);

        var rootTag = new NbtCompound { Name = string.Empty };
        rootTag.Add(serverList);

        var file = new NbtFile(rootTag);
        using var stream = File.Create(serversPath);
        file.SaveToStream(stream, NbtCompression.None);
        return true;
    }

    private async Task InstallInstanceResourceFromFileAsync()
    {
        var activityTitle = SD("instance.content.resource.actions.install_from_file");
        if (!_instanceComposition.Selection.HasSelection)
        {
            AddActivity(activityTitle, SD("instance.content.resource.messages.no_instance_selected"));
            return;
        }

        var (typeName, patterns) = ResolveInstanceResourcePickerOptions();

        string? sourcePath;
        try
        {
            sourcePath = await _shellActionService.PickOpenFileAsync(
                SD("instance.content.resource.dialogs.install_from_file.title", ("surface_title", InstanceResourceSurfaceTitle)),
                typeName,
                patterns);
        }
        catch (Exception ex)
        {
            AddFailureActivity(T("common.activities.failed", ("title", activityTitle)), ex.Message);
            return;
        }

        if (string.IsNullOrWhiteSpace(sourcePath))
        {
            AddActivity(activityTitle, SD("instance.content.resource.messages.install_from_file_canceled"));
            return;
        }

        var targetDirectory = GetCurrentInstanceResourceDirectory();
        Directory.CreateDirectory(targetDirectory);
        var targetPath = GetUniqueChildPath(targetDirectory, Path.GetFileName(sourcePath));
        File.Copy(sourcePath, targetPath, overwrite: false);

        ReloadInstanceComposition();
        AddActivity(activityTitle, $"{sourcePath} -> {targetPath}");
    }

    private void DownloadInstanceResource()
    {
        NavigateTo(
            new LauncherFrontendRoute(LauncherFrontendPageKey.Download, ResolveInstanceDownloadSubpage()),
            SD("instance.content.resource.messages.open_download_route", ("surface_title", InstanceResourceSurfaceTitle)));
    }

    private async Task SetSelectedInstanceResourcesEnabledAsync(bool isEnabled)
    {
        var activityTitle = SD(isEnabled ? "instance.content.resource.actions.enable" : "instance.content.resource.actions.disable");
        if (!_instanceComposition.Selection.HasSelection)
        {
            AddActivity(activityTitle, SD("instance.content.resource.messages.no_instance_selected"));
            return;
        }

        if (!IsInstanceResourceToggleSupported())
        {
            AddActivity(activityTitle, SD("instance.content.resource.messages.toggle_not_supported", ("surface_title", InstanceResourceSurfaceTitle)));
            return;
        }

        var selectedEntries = GetSelectedInstanceResourceEntries()
            .Select(entry => (Title: entry.Title, Path: entry.Path, IsEnabledState: entry.IsEnabledState))
            .ToArray();
        if (selectedEntries.Length == 0)
        {
            AddActivity(activityTitle, SD("instance.content.resource.messages.no_selected"));
            return;
        }

        await SetInstanceResourceEntriesEnabledAsync(selectedEntries, isEnabled, SD("instance.content.resource.messages.no_selected"));
    }

    private Task SetInstanceResourceEntriesEnabledAsync(
        IReadOnlyList<(string Title, string Path, bool IsEnabledState)> entries,
        bool isEnabled,
        string emptyMessage)
    {
        var activityTitle = SD(isEnabled ? "instance.content.resource.actions.enable" : "instance.content.resource.actions.disable");
        if (entries.Count == 0)
        {
            AddActivity(activityTitle, emptyMessage);
            return Task.CompletedTask;
        }

        var candidates = entries
            .Where(entry => entry.IsEnabledState != isEnabled)
            .ToArray();
        if (candidates.Length == 0)
        {
            AddActivity(activityTitle, SD(isEnabled ? "instance.content.resource.messages.already_enabled" : "instance.content.resource.messages.already_disabled"));
            return Task.CompletedTask;
        }

        var succeededEntries = new List<string>();
        var failedEntries = new List<string>();

        foreach (var entry in candidates)
        {
            try
            {
                SetInstanceResourceEnabled(entry.Path, isEnabled);
                succeededEntries.Add(entry.Title);
            }
            catch (Exception ex)
            {
                failedEntries.Add($"{entry.Title}: {ex.Message}");
            }
        }

        ReloadInstanceComposition();
        if (succeededEntries.Count > 0)
        {
            AddActivity(
                activityTitle,
                failedEntries.Count == 0
                    ? SD(
                        isEnabled ? "instance.content.resource.messages.enabled_completed" : "instance.content.resource.messages.disabled_completed",
                        ("count", succeededEntries.Count),
                        ("titles", string.Join(SD("common.punctuation.comma"), succeededEntries)))
                    : SD(
                        isEnabled ? "instance.content.resource.messages.enabled_partial" : "instance.content.resource.messages.disabled_partial",
                        ("count", succeededEntries.Count),
                        ("failed_count", failedEntries.Count)));
        }

        if (failedEntries.Count > 0)
        {
            AddFailureActivity(T("common.activities.failed", ("title", activityTitle)), string.Join(Environment.NewLine, failedEntries));
        }

        return Task.CompletedTask;
    }

    private async Task DeleteSelectedInstanceResourcesAsync()
    {
        var activityTitle = SD("instance.content.resource.actions.delete");
        if (!_instanceComposition.Selection.HasSelection)
        {
            AddActivity(activityTitle, SD("instance.content.resource.messages.no_instance_selected"));
            return;
        }

        var selectedEntries = GetSelectedInstanceResourceEntries()
            .Select(entry => (Title: entry.Title, Path: entry.Path))
            .ToArray();
        if (selectedEntries.Length == 0)
        {
            AddActivity(activityTitle, SD("instance.content.resource.messages.no_selected"));
            return;
        }

        await DeleteInstanceResourcesAsync(selectedEntries, SD("instance.content.resource.messages.no_selected"));
    }

    private async Task DeleteInstanceResourcesAsync(
        IReadOnlyList<(string Title, string Path)> entries,
        string emptyMessage)
    {
        var activityTitle = SD("instance.content.resource.actions.delete");
        if (entries.Count == 0)
        {
            AddActivity(activityTitle, emptyMessage);
            return;
        }

        var itemDescription = string.Join(Environment.NewLine, entries.Take(8).Select(entry => $"- {entry.Title}"));
        if (entries.Count > 8)
        {
            itemDescription = $"{itemDescription}{Environment.NewLine}{SD("instance.content.resource.dialogs.delete.extra_items", ("count", entries.Count - 8))}";
        }

        var confirmed = await ShowToolboxConfirmationAsync(
            SD("instance.content.resource.dialogs.delete.title"),
            $"{SD("instance.content.resource.dialogs.delete.message", ("count", entries.Count), ("surface_title", InstanceResourceSurfaceTitle))}{Environment.NewLine}{Environment.NewLine}{itemDescription}",
            SD("instance.content.resource.dialogs.delete.confirm"),
            isDanger: true);
        if (confirmed != true)
        {
            if (confirmed == false)
            {
                AddActivity(activityTitle, SD("instance.content.resource.messages.delete_canceled"));
            }

            return;
        }

        var trashDirectory = ResolveInstanceResourceTrashDirectory();
        Directory.CreateDirectory(trashDirectory);

        var succeededEntries = new List<string>();
        var failedEntries = new List<string>();
        foreach (var entry in entries)
        {
            try
            {
                MoveInstanceResourceToTrash(entry.Path, trashDirectory);
                succeededEntries.Add(entry.Title);
            }
            catch (Exception ex)
            {
                failedEntries.Add($"{entry.Title}: {ex.Message}");
            }
        }

        ReloadInstanceComposition();
        if (succeededEntries.Count > 0)
        {
            AddActivity(activityTitle, SD("instance.content.resource.messages.deleted_completed", ("count", succeededEntries.Count)));
        }

        if (failedEntries.Count > 0)
        {
            AddFailureActivity(T("common.activities.failed", ("title", activityTitle)), string.Join(Environment.NewLine, failedEntries));
        }
    }

    private void ExportInstanceResourceInfo()
    {
        var activityTitle = SD("instance.content.resource.actions.export_info");
        if (!_instanceComposition.Selection.HasSelection)
        {
            AddActivity(activityTitle, SD("instance.content.resource.messages.no_instance_selected"));
            return;
        }

        var entries = GetCurrentInstanceResourceState().Entries;
        var exportDirectory = Path.Combine(_shellActionService.RuntimePaths.FrontendArtifactDirectory, "instance-resources");
        Directory.CreateDirectory(exportDirectory);
        var outputPath = Path.Combine(
            exportDirectory,
            $"{_instanceComposition.Selection.InstanceName}-{ResolveInstanceResourceExportSlug()}-info.txt");
        var lines = entries.Count == 0
            ? [SD("instance.content.resource.messages.list_empty", ("surface_title", InstanceResourceSurfaceTitle))]
            : entries.Select(entry => $"{entry.Title} | {entry.Meta} | {entry.Summary} | {entry.Path}").ToArray();
        File.WriteAllText(outputPath, string.Join(Environment.NewLine, lines), new UTF8Encoding(false));
        OpenInstanceTarget(activityTitle, outputPath, SD("instance.content.resource.messages.export_missing"));
    }

    private void SetInstanceResourceEnabled(string path, bool isEnabled)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new InvalidOperationException("The resource path is empty.");
        }

        if (!File.Exists(path))
        {
            throw new FileNotFoundException("The resource file does not exist.", path);
        }

        if (isEnabled)
        {
            var enabledFileName = Path.GetFileName(path);
            if (enabledFileName.EndsWith(".disabled", StringComparison.OrdinalIgnoreCase))
            {
                enabledFileName = enabledFileName[..^".disabled".Length];
            }
            else if (enabledFileName.EndsWith(".old", StringComparison.OrdinalIgnoreCase))
            {
                enabledFileName = enabledFileName[..^".old".Length];
            }

            var enabledPath = GetUniqueChildPath(Path.GetDirectoryName(path)!, enabledFileName);
            File.Move(path, enabledPath);
            return;
        }

        var disabledFileName = $"{Path.GetFileName(path)}.disabled";
        var disabledPath = GetUniqueChildPath(Path.GetDirectoryName(path)!, disabledFileName);
        File.Move(path, disabledPath);
    }

    private void CheckInstanceMods() => _ = CheckInstanceModsAsync();

    private async Task CheckInstanceModsAsync()
    {
        var activityTitle = SD("instance.content.resource.actions.check_mods");
        if (!_instanceComposition.Selection.HasSelection)
        {
            AddActivity(activityTitle, SD("instance.content.resource.messages.no_instance_selected"));
            return;
        }

        var enabledMods = _instanceComposition.Mods.Entries;
        var disabledMods = _instanceComposition.DisabledMods.Entries;
        var duplicateGroups = enabledMods
            .Concat(disabledMods)
            .GroupBy(entry => entry.Title, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var lines = new List<string>
        {
            $"{SD("instance.content.resource.check_mods.instance")}: {_instanceComposition.Selection.InstanceName}",
            $"{SD("instance.content.resource.check_mods.enabled_mods")}: {enabledMods.Count}",
            $"{SD("instance.content.resource.check_mods.disabled_mods")}: {disabledMods.Count}",
            $"{SD("instance.content.resource.check_mods.duplicate_names")}: {duplicateGroups.Length}",
            string.Empty
        };

        if (duplicateGroups.Length == 0)
        {
            lines.Add(SD("instance.content.resource.check_mods.no_duplicates"));
        }
        else
        {
            lines.Add(SD("instance.content.resource.check_mods.duplicate_group_title"));
            foreach (var group in duplicateGroups)
            {
                lines.Add($"- {group.Key}");
                foreach (var entry in group)
                {
                    lines.Add($"  {entry.Meta} | {entry.Summary} | {entry.Path}");
                }
            }
        }

        var result = await ShowToolboxConfirmationAsync(activityTitle, string.Join(Environment.NewLine, lines));
        if (result is null)
        {
            return;
        }

        AddActivity(
            activityTitle,
            SD(
                "instance.content.resource.messages.checked_mods_summary",
                ("instance_name", _instanceComposition.Selection.InstanceName),
                ("enabled_count", enabledMods.Count),
                ("disabled_count", disabledMods.Count),
                ("duplicate_count", duplicateGroups.Length)));
    }

    private void ViewInstanceServer(FrontendInstanceServerEntry entry)
    {
        OpenMinecraftServerInspector(entry.Address);
    }

    private FrontendInstanceResourceState GetCurrentInstanceResourceState()
    {
        return new FrontendInstanceResourceState(GetCurrentInstanceResourceSourceEntries());
    }

    private string ResolveInstanceResourceSurfaceTitle()
    {
        return _currentRoute.Subpage switch
        {
            LauncherFrontendSubpageKey.VersionMod => "Mod",
            LauncherFrontendSubpageKey.VersionModDisabled => "Mod",
            LauncherFrontendSubpageKey.VersionResourcePack => SD("instance.content.resource.kind.resource_pack"),
            LauncherFrontendSubpageKey.VersionShader => SD("instance.content.resource.kind.shader"),
            LauncherFrontendSubpageKey.VersionSchematic => SD("instance.content.resource.kind.schematic_file"),
            _ => SD("instance.content.resource.kind.resource")
        };
    }

    private string GetCurrentInstanceResourceDirectory()
    {
        var folderName = _currentRoute.Subpage switch
        {
            LauncherFrontendSubpageKey.VersionResourcePack => "resourcepacks",
            LauncherFrontendSubpageKey.VersionShader => "shaderpacks",
            LauncherFrontendSubpageKey.VersionSchematic => "schematics",
            _ => "mods"
        };

        return ResolveCurrentInstanceResourceDirectory(folderName);
    }

    private (string TypeName, string[] Patterns) ResolveInstanceResourcePickerOptions()
    {
        return _currentRoute.Subpage switch
        {
            LauncherFrontendSubpageKey.VersionResourcePack => (SD("instance.content.resource.dialogs.install_from_file.file_types.resource_pack"), ["*.zip", "*.rar"]),
            LauncherFrontendSubpageKey.VersionShader => (SD("instance.content.resource.dialogs.install_from_file.file_types.shader"), ["*.zip", "*.rar"]),
            LauncherFrontendSubpageKey.VersionSchematic => (SD("instance.content.resource.dialogs.install_from_file.file_types.schematic"), ["*.litematic", "*.schem", "*.schematic", "*.nbt"]),
            _ => (SD("instance.content.resource.dialogs.install_from_file.file_types.mod"), ["*.jar", "*.disabled", "*.old"])
        };
    }

    private LauncherFrontendSubpageKey ResolveInstanceDownloadSubpage()
    {
        return _currentRoute.Subpage switch
        {
            LauncherFrontendSubpageKey.VersionResourcePack => LauncherFrontendSubpageKey.DownloadResourcePack,
            LauncherFrontendSubpageKey.VersionShader => LauncherFrontendSubpageKey.DownloadShader,
            LauncherFrontendSubpageKey.VersionSchematic => LauncherFrontendSubpageKey.DownloadMod,
            _ => LauncherFrontendSubpageKey.DownloadMod
        };
    }

    private string ResolveInstanceResourceExportSlug()
    {
        return _currentRoute.Subpage switch
        {
            LauncherFrontendSubpageKey.VersionMod => "mods",
            LauncherFrontendSubpageKey.VersionModDisabled => "mods",
            LauncherFrontendSubpageKey.VersionResourcePack => "resourcepacks",
            LauncherFrontendSubpageKey.VersionShader => "shaderpacks",
            LauncherFrontendSubpageKey.VersionSchematic => "schematics",
            _ => "resources"
        };
    }

    private string ResolveInstanceResourceTrashDirectory()
    {
        return Path.Combine(
            _instanceComposition.Selection.LauncherDirectory,
            ".pcl-trash",
            "resources",
            ResolveInstanceResourceExportSlug());
    }

    private static void MoveInstanceResourceToTrash(string sourcePath, string trashDirectory)
    {
        if (string.IsNullOrWhiteSpace(sourcePath))
        {
            throw new InvalidOperationException("The resource path is empty.");
        }

        if (!File.Exists(sourcePath) && !Directory.Exists(sourcePath))
        {
            throw new FileNotFoundException("The resource entry does not exist.", sourcePath);
        }

        Directory.CreateDirectory(trashDirectory);

        var timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        var targetPath = GetUniqueChildPath(
            trashDirectory,
            $"{timestamp}-{Path.GetFileName(sourcePath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))}");

        if (Directory.Exists(sourcePath))
        {
            Directory.Move(sourcePath, targetPath);
            return;
        }

        File.Move(sourcePath, targetPath);
    }

    private static IEnumerable<string> ParseClipboardPaths(string? clipboardText)
    {
        if (string.IsNullOrWhiteSpace(clipboardText))
        {
            return [];
        }

        return clipboardText
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim().Trim('"'))
            .Where(line => !string.IsNullOrWhiteSpace(line));
    }

    private static string GetUniqueChildPath(string directory, string fileOrFolderName)
    {
        var candidate = Path.Combine(directory, fileOrFolderName);
        if (!File.Exists(candidate) && !Directory.Exists(candidate))
        {
            return candidate;
        }

        var baseName = Path.GetFileNameWithoutExtension(fileOrFolderName);
        var extension = Path.GetExtension(fileOrFolderName);
        var suffix = 1;
        while (true)
        {
            candidate = Path.Combine(directory, $"{baseName}-{suffix}{extension}");
            if (!File.Exists(candidate) && !Directory.Exists(candidate))
            {
                return candidate;
            }

            suffix++;
        }
    }

    private static void CopyDirectory(string sourceDirectory, string targetDirectory)
    {
        Directory.CreateDirectory(targetDirectory);
        foreach (var file in Directory.EnumerateFiles(sourceDirectory, "*", SearchOption.TopDirectoryOnly))
        {
            var targetPath = Path.Combine(targetDirectory, Path.GetFileName(file));
            File.Copy(file, targetPath, overwrite: false);
        }

        foreach (var directory in Directory.EnumerateDirectories(sourceDirectory, "*", SearchOption.TopDirectoryOnly))
        {
            CopyDirectory(directory, Path.Combine(targetDirectory, Path.GetFileName(directory)));
        }
    }

    private static bool MatchesSearch(params string[] values)
    {
        if (values.Length == 0)
        {
            return true;
        }

        var query = values[^1];
        if (string.IsNullOrWhiteSpace(query))
        {
            return true;
        }

        return values
            .Take(values.Length - 1)
            .Any(value => !string.IsNullOrWhiteSpace(value)
                && value.Contains(query, StringComparison.OrdinalIgnoreCase));
    }
}
