using System.Collections.Generic;
using System.Collections.ObjectModel;
using PCL.Core.App.Essentials;
using PCL.Frontend.Avalonia.ViewModels.Panes;
using PCL.Frontend.Avalonia.Workflows;

namespace PCL.Frontend.Avalonia.ViewModels;

internal sealed partial class LauncherViewModel : ViewModelBase
{
    public event EventHandler<NavigationTransitionEventArgs>? NavigationTransitionRequested;

    public ObservableCollection<NavigationEntryViewModel> TopLevelEntries { get; } = [];

    public ObservableCollection<NavigationEntryViewModel> SidebarEntries { get; } = [];

    public ObservableCollection<SidebarSectionViewModel> SidebarSections { get; } = [];

    public ObservableCollection<NavigationEntryViewModel> UtilityEntries { get; } = [];

    public ObservableCollection<SurfaceFactViewModel> SurfaceFacts { get; } = [];

    public ObservableCollection<SurfaceSectionViewModel> SurfaceSections { get; } = [];

    public ObservableCollection<ActivityItemViewModel> ActivityEntries { get; } = [];

    public ObservableCollection<PromptLaneViewModel> PromptLanes { get; } = [];

    public ObservableCollection<PromptCardViewModel> ActivePrompts { get; } = [];

    public ObservableCollection<LaunchProfileEntryViewModel> LaunchProfileEntries { get; } = [];

    public ObservableCollection<AboutEntryViewModel> AboutProjectEntries { get; } = [];

    public ObservableCollection<AboutEntryViewModel> AboutAcknowledgementEntries { get; } = [];

    public ObservableCollection<FeedbackSectionViewModel> FeedbackSections { get; } = [];

    public ObservableCollection<SimpleListEntryViewModel> LogEntries { get; } = [];

    public ObservableCollection<ToolboxActionViewModel> ToolboxActions { get; } = [];

    public ObservableCollection<SurfaceNoticeViewModel> DownloadInstallHints { get; } = [];

    public ObservableCollection<DownloadInstallMinecraftSectionViewModel> DownloadInstallMinecraftSections { get; } = [];

    public ObservableCollection<DownloadInstallOptionCardViewModel> DownloadInstallOptionCards { get; } = [];

    public ObservableCollection<DownloadInstallOptionViewModel> DownloadInstallOptions { get; } = [];

    public ObservableCollection<DownloadCatalogActionViewModel> DownloadCatalogIntroActions { get; } = [];

    public ObservableCollection<DownloadCatalogSectionViewModel> DownloadCatalogSections { get; } = [];

    public ObservableCollection<DownloadFavoriteSectionViewModel> DownloadFavoriteSections { get; } = [];

    public ObservableCollection<KeyValueEntryViewModel> VersionSaveInfoEntries { get; } = [];

    public ObservableCollection<KeyValueEntryViewModel> VersionSaveSettingEntries { get; } = [];

    public ObservableCollection<SimpleListEntryViewModel> VersionSaveBackupEntries { get; } = [];

    public ObservableCollection<InstanceResourceEntryViewModel> VersionSaveDatapackEntries { get; } = [];

    public ObservableCollection<SurfaceNoticeViewModel> InstanceInstallHints { get; } = [];

    public ObservableCollection<DownloadInstallOptionCardViewModel> InstanceInstallOptionCards { get; } = [];

    public ObservableCollection<DownloadInstallOptionViewModel> InstanceInstallOptions { get; } = [];

    public ObservableCollection<SimpleListEntryViewModel> InstanceWorldEntries { get; } = [];

    public ObservableCollection<InstanceScreenshotEntryViewModel> InstanceScreenshotEntries { get; } = [];

    public ObservableCollection<InstanceServerEntryViewModel> InstanceServerEntries { get; } = [];

    public ObservableCollection<InstanceResourceEntryViewModel> InstanceResourceEntries => GetCurrentInstanceResourceSurfaceState().Entries;

    public ObservableCollection<HelpTopicGroupViewModel> HelpTopicGroups { get; } = [];

    public ObservableCollection<HelpTopicViewModel> HelpSearchResults { get; } = [];

    public ObservableCollection<JavaRuntimeEntryViewModel> JavaRuntimeEntries { get; } = [];

    public ObservableCollection<UiFeatureToggleGroupViewModel> UiFeatureToggleGroups { get; } = [];

    public string ScenarioLabel { get; }

    public string EnvironmentLabel { get; }

    public string InputLabel { get; }

    public string HelpSearchQuery
    {
        get => _helpSearchQuery;
        set
        {
            if (SetProperty(ref _helpSearchQuery, value))
            {
                RefreshHelpTopics();
            }
        }
    }

    public string Eyebrow
    {
        get => _eyebrow;
        private set => SetProperty(ref _eyebrow, value);
    }

    public string Title
    {
        get => _title;
        private set => SetProperty(ref _title, value);
    }

    public string Description
    {
        get => _description;
        private set => SetProperty(ref _description, value);
    }

    public string Status
    {
        get => _status;
        private set => SetProperty(ref _status, value);
    }

    public string BreadcrumbTrail
    {
        get => _breadcrumbTrail;
        private set => SetProperty(ref _breadcrumbTrail, value);
    }

    public string SurfaceMeta
    {
        get => _surfaceMeta;
        private set => SetProperty(ref _surfaceMeta, value);
    }

    public string PromptInboxTitle
    {
        get => _promptInboxTitle;
        private set => SetProperty(ref _promptInboxTitle, value);
    }

    public string PromptInboxSummary
    {
        get => _promptInboxSummary;
        private set => SetProperty(ref _promptInboxSummary, value);
    }

    public string PromptEmptyState
    {
        get => _promptEmptyState;
        private set => SetProperty(ref _promptEmptyState, value);
    }

    public bool IsLaunchRoute => _currentRoute.Page == LauncherFrontendPageKey.Launch;

    public bool IsStandardRoute => !IsLaunchRoute;

    public bool ShowTopLevelNavigation => !CanGoBack;

    public bool IsTopLevelNavigationInteractive => !IsWelcomeOverlayVisible && !IsPromptOverlayVisible && !IsLaunchDialogVisible;

    public bool ShowInnerNavigation => CanGoBack;

    public bool ShowHomeNavigation => CanGoHome;

    public bool IsContextModeRoute => _currentNavigation?.ShowsBackButton ?? false;

    public bool ShowWindowUtilityButtons => !IsContextModeRoute;

    public bool HasRunningTaskManagerTasks => LauncherFrontendRuntimeStateService.HasRunningTasks();

    public bool ShowTaskManagerShortcutButton => !ShowWindowUtilityButtons && HasRunningTaskManagerTasks && !ShowTaskManagerSurface;

    public bool ShowBottomRightUtilityEntryButtons => ShowWindowUtilityButtons && HasUtilityEntries;

    public bool ShowBottomRightPromptQueueButton => ShowWindowUtilityButtons;

    public bool ShowBottomRightExtraButtons => ShowWindowUtilityButtons || ShowTaskManagerShortcutButton;

    public bool ShowMaximizeButton => false;

    public bool HasActivePrompts => ActivePrompts.Count > 0;

    public bool HasNoActivePrompts => !HasActivePrompts;

    public PromptCardViewModel? CurrentPrompt => ActivePrompts.Count > 0 ? ActivePrompts[0] : null;

    public bool HasCurrentPrompt => CurrentPrompt is not null;

    public bool HasSidebarEntries => SidebarEntries.Count > 0;

    public bool HasSidebarSections => SidebarSections.Count > 0;

    public bool HasNoSidebarSections => !HasSidebarSections;

    public bool HasSurfaceFacts => SurfaceFacts.Count > 0;

    public bool HasSurfaceSections => SurfaceSections.Count > 0;

    public bool HasActivityEntries => ActivityEntries.Count > 0;

    public bool HasUtilityEntries => UtilityEntries.Count > 0;

    public bool HasAboutProjectEntries => AboutProjectEntries.Count > 0;

    public bool HasAboutAcknowledgementEntries => AboutAcknowledgementEntries.Count > 0;

    public bool HasFeedbackSections => FeedbackSections.Count > 0;

    public bool HasHelpTopicGroups => HelpTopicGroups.Count > 0;

    public bool HasNoHelpTopicGroups => !HasHelpTopicGroups;

    public bool IsHelpSearchActive => !string.IsNullOrWhiteSpace(HelpSearchQuery);

    public bool ShowHelpTopicLibrary => !IsHelpSearchActive;

    public bool HasHelpSearchResults => HelpSearchResults.Count > 0;

    public bool HasNoHelpSearchResults => !HasHelpSearchResults;

    public string HelpSearchResultsHeader => HasHelpSearchResults
        ? LT("shell.tools.help.search.results_header")
        : LT("shell.tools.help.search.empty_header");

    public string TitleBarLabel => _currentNavigation is null
        ? Title
        : LauncherLocalizationService.ResolveTitleBarLabel(_currentNavigation, _i18n);

    public double StandardLeftPaneWidth => CurrentStandardLeftPaneDescriptor?.Kind switch
    {
        StandardLeftPaneKind.InstanceSelection => 276,
        StandardLeftPaneKind.TaskManager => 200,
        StandardLeftPaneKind.None => 0,
        StandardLeftPaneKind.Sidebar => _standardSidebarAutoWidth,
        _ => 236
    };

    public bool ShowStandardLeftPane => CurrentStandardLeftPaneDescriptor?.Kind != StandardLeftPaneKind.None;

    public double CurrentLeftPaneWidth => IsLaunchRoute
        ? 300
        : StandardLeftPaneWidth;

    internal void SetStandardSidebarAutoWidth(double width)
    {
        var clamped = Math.Clamp(width, 152, 320);
        if (Math.Abs(_standardSidebarAutoWidth - clamped) < 0.5)
        {
            return;
        }

        _standardSidebarAutoWidth = clamped;
        RaisePropertyChanged(nameof(StandardLeftPaneWidth));
        RaisePropertyChanged(nameof(CurrentLeftPaneWidth));
    }

    internal string LT(string key)
    {
        return _i18n.T(key);
    }

    internal string LT(string key, params (string Name, object? Value)[] args)
    {
        if (args.Length == 0)
        {
            return _i18n.T(key);
        }

        var values = new Dictionary<string, object?>(args.Length, StringComparer.Ordinal);
        foreach (var (name, value) in args)
        {
            values[name] = value;
        }

        return _i18n.T(key, values);
    }

    public bool CanGoBack
    {
        get => _canGoBack;
        private set
        {
            if (SetProperty(ref _canGoBack, value))
            {
                _backCommand.NotifyCanExecuteChanged();
                RaisePropertyChanged(nameof(ShowTopLevelNavigation));
                RaisePropertyChanged(nameof(ShowInnerNavigation));
            }
        }
    }

    public bool CanGoHome
    {
        get => _canGoHome;
        private set
        {
            if (SetProperty(ref _canGoHome, value))
            {
                _homeCommand.NotifyCanExecuteChanged();
                RaisePropertyChanged(nameof(ShowHomeNavigation));
            }
        }
    }

    private bool IsCurrentStandardRightPane(StandardRightPaneKind kind)
    {
        return CurrentStandardRightPaneDescriptor?.Kind == kind;
    }
}
