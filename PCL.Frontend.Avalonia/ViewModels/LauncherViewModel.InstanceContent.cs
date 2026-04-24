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
using PCL.Frontend.Avalonia.ViewModels.Panes;
using PCL.Frontend.Avalonia.Workflows;

namespace PCL.Frontend.Avalonia.ViewModels;

internal sealed partial class LauncherViewModel
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

    private sealed class InstanceResourceSurfaceState
    {
        public ObservableCollection<InstanceResourceEntryViewModel> Entries { get; } = [];
        public bool HasRefreshSnapshot { get; set; }
        public IReadOnlyList<FrontendInstanceResourceEntry>? RefreshSourceEntries { get; set; }
        public string RefreshSearchQuery { get; set; } = string.Empty;
        public InstanceResourceFilter RefreshFilter { get; set; } = InstanceResourceFilter.All;
        public InstanceResourceSortMethod RefreshSortMethod { get; set; } = InstanceResourceSortMethod.ResourceName;
        public IReadOnlyList<FrontendInstanceResourceEntry>? CachedModSourceEntries { get; set; }
        public IReadOnlyList<FrontendInstanceResourceEntry>? CachedEnabledModEntries { get; set; }
        public IReadOnlyList<FrontendInstanceResourceEntry>? CachedDisabledModEntries { get; set; }
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
    private bool _isInstanceResourceLoading;
    private InstanceWorldSortMethod _instanceWorldSortMethod = InstanceWorldSortMethod.FileName;
    private InstanceResourceSortMethod _instanceResourceSortMethod = InstanceResourceSortMethod.ResourceName;
    private readonly Dictionary<LauncherFrontendSubpageKey, InstanceResourceSurfaceState> _instanceResourceSurfaceStates = new();
    private readonly HashSet<string> _instanceResourceSelectedPaths = new(StringComparer.OrdinalIgnoreCase);
    private bool _suppressInstanceResourceSelectionChanged;

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

    public bool ShowInstanceResourceLoadingCard => _isInstanceResourceLoading;

    public string InstanceResourceLoadingText => T("download.resource.surface.loading", ("surface_name", InstanceResourceSurfaceTitle));

    public bool ShowInstanceResourceContent => !_isInstanceResourceLoading
        && !ShowInstanceResourceUnsupportedState
        && GetCurrentInstanceResourceSourceEntries().Count > 0;

    public bool ShowInstanceResourceEmptyState => !_isInstanceResourceLoading
        && (ShowInstanceResourceUnsupportedState
            || GetCurrentInstanceResourceSourceEntries().Count == 0);

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

    public ActionCommand RefreshInstanceResourceCommand => new(RefreshInstanceResources);

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

    private static bool IsInstanceResourceSubpage(LauncherFrontendSubpageKey subpage)
    {
        return subpage is LauncherFrontendSubpageKey.VersionMod
            or LauncherFrontendSubpageKey.VersionModDisabled
            or LauncherFrontendSubpageKey.VersionResourcePack
            or LauncherFrontendSubpageKey.VersionShader
            or LauncherFrontendSubpageKey.VersionSchematic;
    }

    private InstanceResourceSurfaceState GetCurrentInstanceResourceSurfaceState()
    {
        if (!_instanceResourceSurfaceStates.TryGetValue(_currentRoute.Subpage, out var state))
        {
            state = new InstanceResourceSurfaceState();
            _instanceResourceSurfaceStates[_currentRoute.Subpage] = state;
        }

        return state;
    }

    private bool ShouldShowInstanceResourceLoadingForRoute(LauncherFrontendRoute route)
    {
        return route.Page == LauncherFrontendPageKey.InstanceSetup
            && IsInstanceResourceSubpage(route.Subpage)
            && ResolveInstanceCompositionLoadMode(route) == FrontendInstanceCompositionService.LoadMode.Full;
    }

    private void SetInstanceResourceLoading(bool isLoading)
    {
        if (_isInstanceResourceLoading == isLoading)
        {
            return;
        }

        _isInstanceResourceLoading = isLoading;
        RaisePropertyChanged(nameof(ShowInstanceResourceLoadingCard));
        RaisePropertyChanged(nameof(ShowInstanceResourceContent));
        RaisePropertyChanged(nameof(ShowInstanceResourceEmptyState));
        RaisePropertyChanged(nameof(ShowInstanceResourceDefaultActions));
        RaisePropertyChanged(nameof(ShowInstanceResourceBatchActions));
    }

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
        if (IsCurrentStandardRightPane(StandardRightPaneKind.InstanceWorld))
        {
            RaisePropertyChanged(nameof(InstanceWorldSearchQuery));
            RaisePropertyChanged(nameof(InstanceWorldSortText));
            RaisePropertyChanged(nameof(HasInstanceWorldEntries));
            RaisePropertyChanged(nameof(HasNoInstanceWorldEntries));
            RaisePropertyChanged(nameof(ShowInstanceWorldContent));
            RaisePropertyChanged(nameof(ShowInstanceWorldEmptyState));
        }

        if (IsCurrentStandardRightPane(StandardRightPaneKind.InstanceScreenshot))
        {
            RaisePropertyChanged(nameof(HasInstanceScreenshotEntries));
            RaisePropertyChanged(nameof(HasNoInstanceScreenshotEntries));
            RaisePropertyChanged(nameof(ShowInstanceScreenshotContent));
            RaisePropertyChanged(nameof(ShowInstanceScreenshotEmptyState));
        }

        if (IsCurrentStandardRightPane(StandardRightPaneKind.InstanceServer))
        {
            RaisePropertyChanged(nameof(InstanceServerSearchQuery));
            RaisePropertyChanged(nameof(HasInstanceServerEntries));
            RaisePropertyChanged(nameof(HasNoInstanceServerEntries));
            RaisePropertyChanged(nameof(ShowInstanceServerContent));
            RaisePropertyChanged(nameof(ShowInstanceServerEmptyState));
        }

        if (IsCurrentStandardRightPane(StandardRightPaneKind.InstanceResource))
        {
            RefreshInstanceResourceEntries();
            RaisePropertyChanged(nameof(InstanceResourceEntries));
            RaisePropertyChanged(nameof(InstanceResourceSearchQuery));
            RaisePropertyChanged(nameof(InstanceResourceSurfaceTitle));
            RaisePropertyChanged(nameof(InstanceResourceLoadingText));
            RaisePropertyChanged(nameof(InstanceResourceSearchWatermark));
            RaisePropertyChanged(nameof(InstanceResourceDownloadButtonText));
            RaisePropertyChanged(nameof(InstanceResourceEmptyTitle));
            RaisePropertyChanged(nameof(InstanceResourceEmptyDescription));
            RaisePropertyChanged(nameof(InstanceResourceEmptyDownloadButtonText));
            RaisePropertyChanged(nameof(ShowInstanceResourceCheckButton));
            RaisePropertyChanged(nameof(ShowInstanceResourceLoadingCard));
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

}
